using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Notification.Models;
using MoAI.Notification.Senders;

namespace MoAI.Notification.Services;

/// <summary>
/// <inheritdoc cref="INotificationService"/>.
/// </summary>
[InjectOnScoped]
public class NotificationService : INotificationService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly NotificationChannelDispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="backgroundJobClient">Hangfire 后台任务客户端.</param>
    /// <param name="dispatcher">渠道发送调度器.</param>
    public NotificationService(DatabaseContext databaseContext, IBackgroundJobClient backgroundJobClient, NotificationChannelDispatcher dispatcher)
    {
        _databaseContext = databaseContext;
        _backgroundJobClient = backgroundJobClient;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc/>
    public async Task<int> NotifyAsync(NotificationMessage message, NotificationSendOptions? options = null, CancellationToken cancellationToken = default)
    {
        var channels = await ResolveChannelsAsync(options, cancellationToken);
        foreach (var channel in channels)
        {
            var log = CreateLog(channel, message);
            log.Status = (int)NotificationSendStatus.Pending;
            _databaseContext.NotificationLogs.Add(log);
            await _databaseContext.SaveChangesAsync(cancellationToken);

            _backgroundJobClient.Enqueue<NotificationSendJob>(job => job.SendAsync(log.Id, channel.Id, message, CancellationToken.None));
        }

        return channels.Count;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationChannelSendResult>> SendNowAsync(NotificationMessage message, NotificationSendOptions? options = null, CancellationToken cancellationToken = default)
    {
        var channels = await ResolveChannelsAsync(options, cancellationToken);
        var results = new List<NotificationChannelSendResult>();
        foreach (var channel in channels)
        {
            var (success, error, durationMs) = await _dispatcher.SendAsync(channel, message, cancellationToken);

            var log = CreateLog(channel, message);
            log.Status = success ? (int)NotificationSendStatus.Success : (int)NotificationSendStatus.Failed;
            log.Error = NotificationText.Truncate(error, 1000);
            log.DurationMs = durationMs;
            _databaseContext.NotificationLogs.Add(log);
            results.Add(new NotificationChannelSendResult
            {
                ChannelId = channel.Id,
                ChannelName = channel.Name,
                Success = success,
                Error = error,
                DurationMs = durationMs
            });
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return results;
    }

    private static NotificationLogEntity CreateLog(NotificationChannelEntity channel, NotificationMessage message)
    {
        return new NotificationLogEntity
        {
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            ChannelType = channel.Type,
            Level = (int)message.Level,
            Title = NotificationText.Truncate(message.Title, 256),
            Content = NotificationText.Truncate(message.Content, 4000),
            Status = (int)NotificationSendStatus.Pending,
            Error = string.Empty
        };
    }

    private async Task<List<NotificationChannelEntity>> ResolveChannelsAsync(NotificationSendOptions? options, CancellationToken cancellationToken)
    {
        var query = _databaseContext.NotificationChannels.AsNoTracking().Where(x => x.Enabled);
        if (options?.ChannelIds is { Count: > 0 })
        {
            var channelIds = options.ChannelIds.ToList();
            query = query.Where(x => channelIds.Contains(x.Id));
        }

        return await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
    }
}
