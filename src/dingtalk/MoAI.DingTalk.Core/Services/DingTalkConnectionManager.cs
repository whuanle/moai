using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DingTalkStream.Core;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoAI.Database;
using MoAI.Database.Redis;
using MoAI.DingTalk.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.DingTalk.Services;

/// <summary>
/// 钉钉 Stream 长连接管理器，单实例；多实例部署时每个钉钉应用（dingtalk_app）一个独立 Redis 租约
/// （<see cref="RedisAppLease"/>），各实例争抢应用租约实现负载均衡——应用分散到不同实例建立 Stream 长连接；
/// 持有者按对账周期续租，实例下线后租约到期自动释放，其它实例争抢接管；
/// 推送经 <see cref="DingTalkEventForwarder"/> 去重并按绑定转发到业务模块.
/// </summary>
[InjectOnSingleton]
public sealed partial class DingTalkConnectionManager : IDingTalkConnectionStatus
{
    private const string LeaseKeyPrefix = "leader:dingtalk-conn:";
    private const string ConnectionStateKey = "connstate:dingtalk-connection";
    private const string InstancesKey = "connstate:dingtalk-instances";
    private const int InstanceHeartbeatTimeout = 15000;
    private static readonly TimeSpan LeaseTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DingTalkEventForwarder _forwarder;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DingTalkConnectionManager> _logger;
    private readonly Dictionary<Guid, DingTalkAppConnection> _connections = new();
    private readonly Dictionary<Guid, string> _lastWrittenState = new();
    private readonly string _instanceId = $"{Environment.MachineName}:{Environment.ProcessId}";
    private readonly CancellationTokenSource _loopCts = new();
    private RedisAppLease? _appLease;
    private Task? _loop;

    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkConnectionManager"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂，用于按需查询数据库.</param>
    /// <param name="forwarder">事件转发器.</param>
    /// <param name="redisDatabase">Redis 数据库（应用租约与连接状态）.</param>
    /// <param name="loggerFactory">日志工厂，用于按客户端类型创建日志器.</param>
    /// <param name="logger">日志.</param>
    public DingTalkConnectionManager(
        IServiceScopeFactory scopeFactory,
        DingTalkEventForwarder forwarder,
        IRedisDatabase redisDatabase,
        ILoggerFactory loggerFactory,
        ILogger<DingTalkConnectionManager> logger)
    {
        _scopeFactory = scopeFactory;
        _forwarder = forwarder;
        _redisDatabase = redisDatabase;
        _loggerFactory = loggerFactory;
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
                _logger.LogWarning(ex, "钉钉连接对账周期异常.");
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
    /// 对账：续租已持有的连接（租约易主则断开，由持有实例处理）、争抢未持有的应用租约（限量分散）、
    /// 按数据库同步新增/删除/禁用/凭证变化，并刷新连接状态哈希供非持有实例的在线状态查询.
    /// </summary>
    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var apps = await databaseContext.DingTalkApps
            .Where(x => !x.IsDisable)
            .Select(x => new { x.Id, x.AppKey, x.AppSecret })
            .ToListAsync(cancellationToken);
        var desiredMap = apps.ToDictionary(x => x.Id);

        List<KeyValuePair<Guid, DingTalkAppConnection>> snapshot;

        lock (_connections)
        {
            snapshot = _connections.ToList();
        }

        // 续租批量流水线发送：两波网络往返完成全部应用的续租，避免逐应用串行往返随应用数线性增长
        var renewTasks = snapshot.ToDictionary(x => x.Key, x => _appLease.RenewAsync(x.Key.ToString("D")));
        await Task.WhenAll(renewTasks.Values);

        var removed = new List<Guid>();
        var lostLease = new List<Guid>();
        var toRestart = new List<(Guid Id, string AppKey, string AppSecret)>();
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
                _logger.LogInformation("钉钉应用连接租约已易主，断开本实例连接，dingTalkAppId={DingTalkAppId}.", item.Key);
                continue;
            }

            if (item.Value.AppKey != app.AppKey || item.Value.AppSecret != app.AppSecret)
            {
                // 凭证变化：断开重建（租约仍归本实例）
                toRestart.Add((item.Key, app.AppKey, app.AppSecret));
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
            .Select(x => (x.Id, x.AppKey, x.AppSecret))
            .ToList();

        foreach (var app in acquired)
        {
            _logger.LogInformation("抢占到钉钉应用连接租约，dingTalkAppId={DingTalkAppId} appKey={AppKey}.", app.Id, app.AppKey);
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
            StartConnection(app.Id, app.AppKey, app.AppSecret);
        }

        foreach (var app in acquired)
        {
            StartConnection(app.Id, app.AppKey, app.AppSecret);
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
                    _logger.LogInformation("钉钉连接超过公平份额（持有 {Owned} / 份额 {FairShare}，存活实例 {Instances}），释放 {Count} 个应用由其它实例争抢.", ownedCount, fairShare, liveInstances, released.Count);
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

    private async Task ReleaseLeaseSafeAsync(Guid dingTalkAppId)
    {
        try
        {
            await _appLease.ReleaseAsync(dingTalkAppId.ToString("D"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放钉钉应用连接租约失败，dingTalkAppId={DingTalkAppId}.", dingTalkAppId);
        }
    }

    /// <summary>
    /// 接入钉钉应用后建立长连接：本实例抢到应用租约即建连；被其它实例抢先时由其对账周期建连.
    /// </summary>
    /// <param name="dingTalkAppId">钉钉应用记录 id.</param>
    /// <param name="appKey">钉钉应用 AppKey.</param>
    /// <param name="appSecret">钉钉应用 AppSecret.</param>
    /// <returns>返回应用任务.</returns>
    public async Task ApplyCreateAsync(Guid dingTalkAppId, string appKey, string appSecret)
    {
        if (_appLease == null || !await _appLease.TryAcquireAsync(dingTalkAppId.ToString("D")))
        {
            return;
        }

        StartConnection(dingTalkAppId, appKey, appSecret);
    }

    /// <summary>
    /// 更新钉钉应用：本实例持有连接则立即重建；其它实例持有时由其对账周期按数据库自动同步.
    /// </summary>
    /// <param name="dingTalkAppId">钉钉应用记录 id.</param>
    /// <param name="appKey">钉钉应用 AppKey.</param>
    /// <param name="appSecret">钉钉应用 AppSecret.</param>
    /// <param name="isDisable">是否禁用，禁用后断开且不再接收事件.</param>
    public void ApplyUpdate(Guid dingTalkAppId, string appKey, string appSecret, bool isDisable)
    {
        lock (_connections)
        {
            if (!_connections.ContainsKey(dingTalkAppId))
            {
                return;
            }

            StopConnectionInternal(dingTalkAppId);
        }

        if (!isDisable)
        {
            StartConnection(dingTalkAppId, appKey, appSecret);
        }
    }

    /// <summary>
    /// 删除钉钉应用：本实例持有连接则断开并释放租约；其它实例持有时由其对账周期自动断开.
    /// </summary>
    /// <param name="dingTalkAppId">钉钉应用记录 id.</param>
    /// <returns>返回应用任务.</returns>
    public async Task ApplyDeleteAsync(Guid dingTalkAppId)
    {
        lock (_connections)
        {
            if (!_connections.ContainsKey(dingTalkAppId))
            {
                return;
            }

            StopConnectionInternal(dingTalkAppId);
        }

        await ReleaseLeaseSafeAsync(dingTalkAppId);
    }

    /// <summary>
    /// 查询长连接是否在线；本实例持有连接时返回本地状态，
    /// 否则（连接被其它实例持有时）读持有实例刷新的 Redis 状态哈希，避免多实例部署时误显示离线.
    /// </summary>
    /// <param name="dingTalkAppId">钉钉应用记录 id.</param>
    /// <returns>返回是否在线.</returns>
    public bool IsOnline(Guid dingTalkAppId)
    {
        lock (_connections)
        {
            if (_connections.TryGetValue(dingTalkAppId, out var connection))
            {
                return connection.IsOnline;
            }
        }

        try
        {
            var value = _redisDatabase.Database.HashGet(ConnectionStateKey, dingTalkAppId.ToString("D"));
            return value == "1";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询钉钉连接状态哈希失败，dingTalkAppId={DingTalkAppId}.", dingTalkAppId);
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

        foreach (var dingTalkAppId in ownedIds)
        {
            StopConnection(dingTalkAppId);
        }

        return Task.CompletedTask;
    }

    private void StartConnection(Guid dingTalkAppId, string appKey, string appSecret)
    {
        lock (_connections)
        {
            if (_connections.ContainsKey(dingTalkAppId))
            {
                return;
            }

            var cts = new CancellationTokenSource();
            var connection = new DingTalkAppConnection(cts, appKey, appSecret);
            _connections[dingTalkAppId] = connection;

            var options = new DingTalkStreamOptions
            {
                ClientId = appKey,
                ClientSecret = appSecret,
                UA = "MoAI",
                AutoReplySystemMessage = true,
                Subscriptions =
                {
                    new Subscription { Type = SubscriptionType.CALLBACK, Topic = DingTalkEventMessage.RobotMessageTopic }
                },
            };

            var client = new DingTalkStreamClient(
                Options.Create(options),
                _loggerFactory.CreateLogger<DingTalkStreamClient>());

            client.OnMessage += (_, args) =>
            {
                // 收帧线程上先 ack 再转异步，避免阻塞收帧
                _forwarder.Forward(dingTalkAppId, appKey, args);
            };
            client.OnStarted += (_, _) =>
            {
                connection.IsOnline = true;
                _logger.LogInformation("钉钉长连接已就绪，dingTalkAppId={DingTalkAppId} appKey={AppKey}.", dingTalkAppId, appKey);
            };
            client.OnStoped += (_, ex) =>
            {
                connection.IsOnline = false;
                if (ex != null)
                {
                    _logger.LogWarning(ex, "钉钉长连接已停止，dingTalkAppId={DingTalkAppId} appKey={AppKey}.", dingTalkAppId, appKey);
                }
            };

            connection.Client = client;
            connection.RunTask = Task.Run(async () =>
            {
                try
                {
                    // Start 在连接成功建立后即返回（生命周期由 SDK 内部维持，断线自动重连），
                    // 这里只用于观察启动阶段失败：凭证错误时 SDK 在内部重试循环中挂起（Start 不返回），
                    // 取消时抛 OperationCanceledException。在线状态完全由 OnStarted/OnStoped 事件维护，
                    // 不能在此处 finally 置离线——那会在连接成功后立即把状态打回离线。
                    await client.Start(cts.Token);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "钉钉长连接启动任务异常退出，dingTalkAppId={DingTalkAppId} appKey={AppKey}.", dingTalkAppId, appKey);
                }
            });
        }
    }

    private void StopConnection(Guid dingTalkAppId)
    {
        lock (_connections)
        {
            StopConnectionInternal(dingTalkAppId);
        }
    }

    private void StopConnectionInternal(Guid dingTalkAppId)
    {
        if (!_connections.Remove(dingTalkAppId, out var connection))
        {
            return;
        }

        connection.IsOnline = false;
        try
        {
            connection.Cts.Cancel();
            connection.Client?.StopAsync().GetAwaiter().GetResult();
            connection.Client?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开钉钉长连接异常，dingTalkAppId={DingTalkAppId}.", dingTalkAppId);
        }
        finally
        {
            connection.Cts.Dispose();
        }
    }
}
