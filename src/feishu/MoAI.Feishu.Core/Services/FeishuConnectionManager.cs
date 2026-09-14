using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FeishuWss;
using FeishuWss.Client;
using FeishuWss.Events;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书长连接管理器，单实例；每个启用的飞书应用（feishu_app）维持一条 WSS 长连接，
/// 事件经 <see cref="FeishuEventForwarder"/> 去重并按绑定转发到业务模块.
/// </summary>
[InjectOnSingleton]
public sealed partial class FeishuConnectionManager
{
    /// <summary>
    /// 飞书默认接入域名.
    /// </summary>
    public const string DefaultDomain = "https://open.feishu.cn";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeishuEventForwarder _forwarder;
    private readonly ILogger<FeishuConnectionManager> _logger;
    private readonly Dictionary<Guid, FeishuAppConnection> _connections = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuConnectionManager"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂，用于按需查询数据库.</param>
    /// <param name="forwarder">事件转发器.</param>
    /// <param name="logger">日志.</param>
    public FeishuConnectionManager(IServiceScopeFactory scopeFactory, FeishuEventForwarder forwarder, ILogger<FeishuConnectionManager> logger)
    {
        _scopeFactory = scopeFactory;
        _forwarder = forwarder;
        _logger = logger;
    }

    /// <summary>
    /// 启动时加载全部启用的飞书应用并逐个建立长连接；单个应用连接失败不影响其它应用与宿主启动.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回初始化任务.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var apps = await databaseContext.FeishuApps
            .Where(x => !x.IsDisable)
            .Select(x => new { x.Id, x.AppId, x.AppSecret, x.Domain })
            .ToListAsync(cancellationToken);

        foreach (var app in apps)
        {
            StartConnection(app.Id, app.AppId, app.AppSecret, app.Domain);
        }

        if (apps.Count > 0)
        {
            _logger.LogInformation("飞书长连接初始化完成，共 {Count} 个应用.", apps.Count);
        }
    }

    /// <summary>
    /// 创建飞书应用后建立长连接（仅在应用未禁用时调用）.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <param name="appId">飞书开放平台 AppID.</param>
    /// <param name="appSecret">飞书开放平台 AppSecret.</param>
    /// <param name="domain">接入域名，空使用默认.</param>
    public void ApplyCreate(Guid feishuAppId, string appId, string appSecret, string? domain)
    {
        StartConnection(feishuAppId, appId, appSecret, domain);
    }

    /// <summary>
    /// 更新飞书应用后重连；禁用时断开且不再连接.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <param name="appId">飞书开放平台 AppID.</param>
    /// <param name="appSecret">飞书开放平台 AppSecret.</param>
    /// <param name="domain">接入域名，空使用默认.</param>
    /// <param name="isDisable">是否禁用.</param>
    public void ApplyUpdate(Guid feishuAppId, string appId, string appSecret, string? domain, bool isDisable)
    {
        StopConnection(feishuAppId);
        if (!isDisable)
        {
            StartConnection(feishuAppId, appId, appSecret, domain);
        }
    }

    /// <summary>
    /// 删除飞书应用后断开长连接.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    public void ApplyDelete(Guid feishuAppId)
    {
        StopConnection(feishuAppId);
    }

    /// <summary>
    /// 查询长连接是否在线.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <returns>返回是否在线.</returns>
    public bool IsOnline(Guid feishuAppId)
    {
        lock (_connections)
        {
            return _connections.TryGetValue(feishuAppId, out var connection) && connection.IsOnline;
        }
    }

    /// <summary>
    /// 停机时断开全部长连接.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回停止任务.</returns>
    public Task StopAllAsync(CancellationToken cancellationToken)
    {
        lock (_connections)
        {
            foreach (var feishuAppId in _connections.Keys.ToList())
            {
                StopConnection(feishuAppId);
            }
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
            var connection = new FeishuAppConnection(cts);
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
                .WithDomain(string.IsNullOrWhiteSpace(domain) ? DefaultDomain : domain)
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
                    _logger.LogError(ex, "飞书长连接异常退出，feishuAppId={FeishuAppId} appId={AppId}.", feishuAppId, appId);
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
}
