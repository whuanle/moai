using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;
using MoAI.Notification.Senders;

namespace MoAI.Notification.Services;

/// <summary>
/// 通知发送任务（Hangfire 一次性任务）：对单个渠道执行投递并回写发送记录，失败自动重试 3 次（至少一次语义）.
/// </summary>
public class NotificationSendJob
{
    private readonly DatabaseContext _databaseContext;
    private readonly NotificationChannelDispatcher _dispatcher;
    private readonly ILogger<NotificationSendJob> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationSendJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="dispatcher">渠道发送调度器.</param>
    /// <param name="logger">日志.</param>
    public NotificationSendJob(DatabaseContext databaseContext, NotificationChannelDispatcher dispatcher, ILogger<NotificationSendJob> logger)
    {
        _databaseContext = databaseContext;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// 发送通知到指定渠道（Hangfire 入口）.
    /// </summary>
    /// <param name="logId">发送记录 id.</param>
    /// <param name="channelId">渠道 id.</param>
    /// <param name="message">通知消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    [AutomaticRetry(Attempts = 3)]
    public async Task SendAsync(long logId, long channelId, NotificationMessage message, CancellationToken cancellationToken)
    {
        var log = await _databaseContext.NotificationLogs.FirstOrDefaultAsync(x => x.Id == logId, cancellationToken);
        if (log == null)
        {
            _logger.LogWarning("通知发送记录不存在，跳过投递：{LogId}.", logId);
            return;
        }

        var channel = await _databaseContext.NotificationChannels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == channelId, cancellationToken);
        if (channel == null || !channel.Enabled)
        {
            log.Status = (int)NotificationSendStatus.Failed;
            log.Error = "渠道不存在或已停用";
            await _databaseContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("通知渠道不存在或已停用，跳过投递：{ChannelId}.", channelId);
            return;
        }

        var (success, error, durationMs) = await _dispatcher.SendAsync(channel, message, cancellationToken);

        log.Status = success ? (int)NotificationSendStatus.Success : (int)NotificationSendStatus.Failed;
        log.Error = NotificationText.Truncate(error, 1000);
        log.DurationMs = durationMs;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (!success)
        {
            // 抛出让 Hangfire 自动重试（ Attempts = 3 ），重试成功会回写为成功
            throw new BusinessException($"通知渠道 [{channel.Name}] 发送失败：{error}") { StatusCode = 502 };
        }

        _logger.LogInformation("通知已投递：{ChannelName}，耗时 {DurationMs}ms.", channel.Name, durationMs);
    }
}
