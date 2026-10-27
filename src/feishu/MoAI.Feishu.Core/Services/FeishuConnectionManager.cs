using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeishuWss;
using FeishuWss.Client;
using FeishuWss.Events;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书长连接管理器，单实例；多实例部署时每个飞书应用（feishu_app）一个独立 Redis 租约
/// （<see cref="RedisAppLease"/>），各实例争抢应用租约实现负载均衡——应用分散到不同实例建立 WSS 长连接；
/// 持有者按对账周期续租，实例下线后租约到期自动释放，其它实例争抢接管；
/// 事件经 <see cref="FeishuEventForwarder"/> 去重并按绑定转发到业务模块.
/// </summary>
[InjectOnSingleton]
public sealed partial class FeishuConnectionManager : IFeishuConnectionStatus
{
    /// <summary>
    /// 飞书默认接入域名.
    /// </summary>
    public const string DefaultDomain = "https://open.feishu.cn";

    private const string LeaseKeyPrefix = "leader:feishu-conn:";
    private const string ConnectionStateKey = "connstate:feishu-connection";
    private const string InstancesKey = "connstate:feishu-instances";
    private const int InstanceHeartbeatTimeout = 15000;
    private static readonly TimeSpan LeaseTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeishuEventForwarder _forwarder;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<FeishuConnectionManager> _logger;
    private readonly Dictionary<Guid, FeishuAppConnection> _connections = new();
    private readonly Dictionary<Guid, string> _lastWrittenState = new();
    private readonly string _instanceId = $"{Environment.MachineName}:{Environment.ProcessId}";
    private readonly CancellationTokenSource _loopCts = new();
    private RedisAppLease? _appLease;
    private Task? _loop;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuConnectionManager"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂，用于按需查询数据库.</param>
    /// <param name="forwarder">事件转发器.</param>
    /// <param name="redisDatabase">Redis 数据库（应用租约与连接状态）.</param>
    /// <param name="logger">日志.</param>
    public FeishuConnectionManager(IServiceScopeFactory scopeFactory, FeishuEventForwarder forwarder, IRedisDatabase redisDatabase, ILogger<FeishuConnectionManager> logger)
    {
        _scopeFactory = scopeFactory;
        _forwarder = forwarder;
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <summary>
    /// 启动对账循环：持续争抢未持有的应用租约、续租已持有的连接，并按数据库对账.
    /// </summary>
    public void StartLease()
    {
        _appLease = new RedisAppLease(_redisDatabase, LeaseKeyPrefix, _instanceId, LeaseTime);
        _loop = Task.Run(() => RunReconcileLoopAsync(_loopCts.Token));
    }

    /// <summary>
    /// 停机：停止对账循环、断开本实例持有的全部连接并释放对应租约（其它实例立即接管）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回停止任务.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _loopCts.Cancel();
        if (_loop != null)
        {
            try
            {
                await _loop;
            }
            catch
            {
                // 停机路径，忽略循环退出异常
            }
        }

        List<Guid> ownedIds;

        lock (_connections)
        {
            ownedIds = _connections.Keys.ToList();
        }

        foreach (var id in ownedIds)
        {
            StopConnection(id);
            await ReleaseLeaseSafeAsync(id);
        }
    }

    private async Task RunReconcileLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "飞书连接对账周期异常.");
            }

            try
            {
                await Task.Delay(ReconcileInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 对账：续租已持有的连接（租约易主则断开，由持有实例处理）、争抢未持有的应用租约、
    /// 按数据库同步新增/删除/禁用/凭证变化，并刷新连接状态哈希供非持有实例的在线状态查询.
    /// Redis 操作按流水线批量发送（WhenAll），每周期网络往返次数与连接数无关.
    /// </summary>
    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var apps = await databaseContext.FeishuApps
            .Where(x => !x.IsDisable)
            .Select(x => new { x.Id, x.AppId, x.AppSecret, x.Domain })
            .ToListAsync(cancellationToken);
        var desiredMap = apps.ToDictionary(x => x.Id);

        List<KeyValuePair<Guid, FeishuAppConnection>> snapshot;

        lock (_connections)
        {
            snapshot = _connections.ToList();
        }

        // 续租批量流水线发送：两波网络往返完成全部应用的续租，避免逐应用串行往返随应用数线性增长
        var renewTasks = snapshot.ToDictionary(x => x.Key, x => _appLease.RenewAsync(x.Key.ToString("D")));
        await Task.WhenAll(renewTasks.Values);

        var removed = new List<Guid>();
        var lostLease = new List<Guid>();
        var toRestart = new List<(Guid Id, string AppId, string AppSecret, string Domain)>();
        var stateWrites = new List<KeyValuePair<Guid, bool>>();

        foreach (var item in snapshot)
        {
            if (!desiredMap.TryGetValue(item.Key, out var app))
            {
                // 应用已禁用/删除：断开并释放租约
                removed.Add(item.Key);
                continue;
            }

            if (!await renewTasks[item.Key])
            {
                // 租约已易主（本实例曾停顿超过租约时长）：断开，应用由持有实例处理
                lostLease.Add(item.Key);
                _logger.LogInformation("飞书应用连接租约已易主，断开本实例连接，feishuAppId={FeishuAppId}.", item.Key);
                continue;
            }

            if (item.Value.AppId != app.AppId || item.Value.AppSecret != app.AppSecret || item.Value.Domain != app.Domain)
            {
                // 凭证/域名变化：断开重建（租约仍归本实例）
                toRestart.Add((item.Key, app.AppId, app.AppSecret, app.Domain));
                continue;
            }

            stateWrites.Add(new KeyValuePair<Guid, bool>(item.Key, item.Value.IsOnline));
        }

        // 争抢未持有的应用租约（无持有者的应用本轮即可建连；
        // 冷启动竞争导致的「先启动实例圈占全部应用」由下方公平份额再均衡自动纠正）
        var ownedIds = snapshot.Select(x => x.Key).Concat(toRestart.Select(x => x.Id)).ToHashSet();
        var candidates = apps.Where(x => !ownedIds.Contains(x.Id)).ToList();
        Shuffle(candidates);

        var acquireTasks = candidates.ToDictionary(x => x.Id, x => _appLease.TryAcquireAsync(x.Id.ToString("D")));
        await Task.WhenAll(acquireTasks.Values);

        var acquired = candidates
            .Where(x => acquireTasks[x.Id].Result)
            .Select(x => (x.Id, x.AppId, x.AppSecret, x.Domain))
            .ToList();

        foreach (var app in acquired)
        {
            _logger.LogInformation("抢占到飞书应用连接租约，feishuAppId={FeishuAppId} appId={AppId}.", app.Id, app.AppId);
        }

        foreach (var id in removed)
        {
            StopConnection(id);
            await ReleaseLeaseSafeAsync(id);
        }

        foreach (var id in lostLease)
        {
            StopConnection(id);
        }

        foreach (var app in toRestart)
        {
            StopConnection(app.Id);
            StartConnection(app.Id, app.AppId, app.AppSecret, app.Domain);
        }

        foreach (var app in acquired)
        {
            StartConnection(app.Id, app.AppId, app.AppSecret, app.Domain);
        }

        // 公平份额再均衡：注册本实例存活心跳并统计存活实例数，
        // 持有数超过 ceil(应用数/实例数)+1 的实例主动释放多余连接（租约释放后由其它实例争抢），
        // 解决「先启动实例在其它实例就绪前圈占全部应用」的冷启动竞争
        var liveInstances = await RegisterInstanceAndCountAsync();
        var ownedCount = snapshot.Count(x => !removed.Contains(x.Key) && !lostLease.Contains(x.Key)) + acquired.Count;
        if (liveInstances > 1)
        {
            var fairShare = (int)Math.Ceiling(apps.Count / (double)liveInstances) + 1;
            var excess = ownedCount - fairShare;
            if (excess > 0)
            {
                var released = new List<Guid>();
                foreach (var item in snapshot)
                {
                    if (excess <= 0)
                    {
                        break;
                    }

                    if (removed.Contains(item.Key) || lostLease.Contains(item.Key) || toRestart.Any(x => x.Id == item.Key))
                    {
                        continue;
                    }

                    released.Add(item.Key);
                    excess--;
                }

                if (released.Count > 0)
                {
                    _logger.LogInformation("飞书连接超过公平份额（持有 {Owned} / 份额 {FairShare}，存活实例 {Instances}），释放 {Count} 个应用由其它实例争抢.", ownedCount, fairShare, liveInstances, released.Count);
                    foreach (var id in released)
                    {
                        StopConnection(id);
                        await ReleaseLeaseSafeAsync(id);
                    }
                }
            }
        }

        stateWrites.AddRange(acquired.Select(x => new KeyValuePair<Guid, bool>(x.Id, false)));

        // 状态哈希：仅写发生变化的字段（流水线批量），键过期随每周期刷新——
        // 全部持有者下线后键过期，其它实例查询即全体离线
        var database = _redisDatabase.Database;
        var changedWrites = new List<KeyValuePair<Guid, bool>>();

        foreach (var item in stateWrites)
        {
            var value = item.Value ? "1" : "0";
            if (!_lastWrittenState.TryGetValue(item.Key, out var last) || last != value)
            {
                changedWrites.Add(item);
            }
        }

        if (stateWrites.Count > 0)
        {
            if (changedWrites.Count > 0)
            {
                await Task.WhenAll(changedWrites.Select(item => database.HashSetAsync(ConnectionStateKey, item.Key.ToString("D"), item.Value ? "1" : "0")));
                foreach (var item in changedWrites)
                {
                    _lastWrittenState[item.Key] = item.Value ? "1" : "0";
                }
            }

            await database.KeyExpireAsync(ConnectionStateKey, LeaseTime * 2);
        }

        foreach (var id in removed.Concat(lostLease))
        {
            _lastWrittenState.Remove(id);
        }
    }

    /// <summary>
    /// 注册本实例存活心跳（写入实例注册哈希，15 秒未刷新视为下线）并返回当前存活实例数；
    /// 同时清理其它实例的过期心跳.
    /// </summary>
    private async Task<int> RegisterInstanceAndCountAsync()
    {
        var database = _redisDatabase.Database;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await database.HashSetAsync(InstancesKey, _instanceId, now.ToString());

        var entries = await database.HashGetAllAsync(InstancesKey);
        foreach (var entry in entries)
        {
            var stale = long.TryParse(entry.Value, out var ms) && now - ms > InstanceHeartbeatTimeout;
            if (stale && entry.Name != _instanceId)
            {
                await database.HashDeleteAsync(InstancesKey, entry.Name);
            }
        }

        entries = await database.HashGetAllAsync(InstancesKey);
        return entries.Count(e => long.TryParse(e.Value, out var ms) && now - ms <= InstanceHeartbeatTimeout);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private async Task ReleaseLeaseSafeAsync(Guid feishuAppId)
    {
        try
        {
            await _appLease.ReleaseAsync(feishuAppId.ToString("D"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放飞书应用连接租约失败，feishuAppId={FeishuAppId}.", feishuAppId);
        }
    }

    /// <summary>
    /// 接入飞书应用后建立长连接：本实例抢到应用租约即建连；被其它实例抢先时由其对账周期建连.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <param name="appId">飞书开放平台 AppID.</param>
    /// <param name="appSecret">飞书开放平台 AppSecret.</param>
    /// <param name="domain">接入域名，空使用默认.</param>
    /// <returns>返回应用任务.</returns>
    public async Task ApplyCreateAsync(Guid feishuAppId, string appId, string appSecret, string? domain)
    {
        if (_appLease == null || !await _appLease.TryAcquireAsync(feishuAppId.ToString("D")))
        {
            return;
        }

        StartConnection(feishuAppId, appId, appSecret, domain);
    }

    /// <summary>
    /// 更新飞书应用：本实例持有连接则立即重建；其它实例持有时由其对账周期按数据库自动同步.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <param name="appId">飞书开放平台 AppID.</param>
    /// <param name="appSecret">飞书开放平台 AppSecret.</param>
    /// <param name="domain">接入域名，空使用默认.</param>
    /// <param name="isDisable">是否禁用，禁用后断开且不再接收事件.</param>
    public void ApplyUpdate(Guid feishuAppId, string appId, string appSecret, string? domain, bool isDisable)
    {
        lock (_connections)
        {
            if (!_connections.ContainsKey(feishuAppId))
            {
                return;
            }

            StopConnectionInternal(feishuAppId);
        }

        if (!isDisable)
        {
            StartConnection(feishuAppId, appId, appSecret, domain);
        }
    }

    /// <summary>
    /// 删除飞书应用：本实例持有连接则断开并释放租约；其它实例持有时由其对账周期自动断开.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <returns>返回应用任务.</returns>
    public async Task ApplyDeleteAsync(Guid feishuAppId)
    {
        lock (_connections)
        {
            if (!_connections.ContainsKey(feishuAppId))
            {
                return;
            }

            StopConnectionInternal(feishuAppId);
        }

        await ReleaseLeaseSafeAsync(feishuAppId);
    }

    /// <summary>
    /// 查询长连接是否在线；本实例持有连接时返回本地状态，
    /// 否则（连接被其它实例持有时）读持有实例刷新的 Redis 状态哈希，避免多实例部署时误显示离线.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <returns>返回是否在线.</returns>
    public bool IsOnline(Guid feishuAppId)
    {
        lock (_connections)
        {
            if (_connections.TryGetValue(feishuAppId, out var connection))
            {
                return connection.IsOnline;
            }
        }

        try
        {
            var value = _redisDatabase.Database.HashGet(ConnectionStateKey, feishuAppId.ToString("D"));
            return value == "1";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询飞书连接状态哈希失败，feishuAppId={FeishuAppId}.", feishuAppId);
            return false;
        }
    }

    /// <summary>
    /// 停机时断开全部长连接（租约释放见 <see cref="StopAsync"/>）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回停止任务.</returns>
    public Task StopAllAsync(CancellationToken cancellationToken)
    {
        List<Guid> ownedIds;

        lock (_connections)
        {
            ownedIds = _connections.Keys.ToList();
        }

        foreach (var feishuAppId in ownedIds)
        {
            StopConnection(feishuAppId);
        }

        return Task.CompletedTask;
    }

    private void StartConnection(Guid feishuAppId, string appId, string appSecret, string? domain)
    {
        lock (_connections)
        {
            if (_connections.ContainsKey(feishuAppId))
            {
                return;
            }

            var cts = new CancellationTokenSource();
            var connection = new FeishuAppConnection(cts, appId, appSecret, string.IsNullOrWhiteSpace(domain) ? DefaultDomain : domain);
            _connections[feishuAppId] = connection;

            var eventDispatcher = new EventDispatcher().OnAny(ctx =>
            {
                // 立即 ack，事件转线程池异步处理，避免阻塞长连接收帧循环
                _forwarder.Forward(feishuAppId, ctx);
                return Task.FromResult<object?>(null);
            });

            var client = WssClient.Build()
                .WithAppId(appId)
                .WithAppSecret(appSecret)
                .WithDomain(connection.Domain)
                .WithEventDispatcher(eventDispatcher)
                .WithLogger(_logger)
                .OnReady(() =>
                {
                    connection.IsOnline = true;
                    _logger.LogInformation("飞书长连接已就绪，feishuAppId={FeishuAppId} appId={AppId}.", feishuAppId, appId);
                    return Task.CompletedTask;
                })
                .OnDisconnected(() =>
                {
                    connection.IsOnline = false;
                    return Task.CompletedTask;
                })
                .OnError(ex =>
                {
                    _logger.LogWarning(ex, "飞书长连接异常，feishuAppId={FeishuAppId} appId={AppId}.", feishuAppId, appId);
                    return Task.CompletedTask;
                })
                .Build();

            connection.Client = client;
            connection.RunTask = Task.Run(async () =>
            {
                try
                {
                    // 阻塞直到连接最终断开或取消，断线由 SDK 自动重连
                    await client.StartAsync(cts.Token);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    // 长连接退出多为 appId/appSecret 配置错误，非业务异常，只记 Warn 不带堆栈，避免错误凭证下刷屏
                    _logger.LogWarning("飞书长连接异常退出（常见于 appId/appSecret 配置错误），feishuAppId={FeishuAppId} appId={AppId} reason={Reason}.", feishuAppId, appId, ex.Message);
                }
                finally
                {
                    connection.IsOnline = false;
                }
            });
        }
    }

    private void StopConnection(Guid feishuAppId)
    {
        lock (_connections)
        {
            StopConnectionInternal(feishuAppId);
        }
    }

    private void StopConnectionInternal(Guid feishuAppId)
    {
        if (!_connections.Remove(feishuAppId, out var connection))
        {
            return;
        }

        connection.IsOnline = false;
        try
        {
            connection.Cts.Cancel();
            connection.Client?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开飞书长连接异常，feishuAppId={FeishuAppId}.", feishuAppId);
        }
    }
}
