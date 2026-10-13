using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoAI.AI.Services;

/// <summary>
/// 应用安全事件落库后台服务：从通道批量读取规则命中事件，攒批写 <c>app_security_event</c>.
/// <para>写失败仅告警（事件缓冲丢弃），不影响宿主运行.</para>
/// </summary>
internal sealed class AppSecurityAuditStoreService : BackgroundService
{
    private const int BatchSize = 50;

    private readonly AppSecurityAuditChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppSecurityAuditStoreService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSecurityAuditStoreService"/> class.
    /// </summary>
    /// <param name="channel">单例事件通道.</param>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="logger">日志.</param>
    public AppSecurityAuditStoreService(AppSecurityAuditChannel channel, IServiceScopeFactory scopeFactory, ILogger<AppSecurityAuditStoreService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<Database.Entities.AppSecurityEventEntity>(BatchSize);
        try
        {
            while (await _channel.Queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                while (_channel.Queue.Reader.TryRead(out var evt))
                {
                    batch.Add(evt);
                    if (batch.Count >= BatchSize)
                    {
                        await FlushAsync(batch, stoppingToken).ConfigureAwait(false);
                        batch = new List<Database.Entities.AppSecurityEventEntity>(BatchSize);
                    }
                }

                if (batch.Count > 0)
                {
                    await FlushAsync(batch, stoppingToken).ConfigureAwait(false);
                    batch = new List<Database.Entities.AppSecurityEventEntity>(BatchSize);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 停机取消：尽力清空缓冲
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task FlushAsync(List<Database.Entities.AppSecurityEventEntity> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            databaseContext.AppSecurityEvents.AddRange(batch);
            await databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "应用安全事件落库失败，丢弃 {Count} 条缓冲事件.", batch.Count);
        }
    }
}
