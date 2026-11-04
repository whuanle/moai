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
using MoAI.Message.Services;
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
    private readonly IMessageService _messageService;
    private readonly NotificationChannelDispatcher _dispatcher;
    private readonly NotificationTemplateApplier _templateApplier;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="backgroundJobClient">Hangfire 后台任务客户端.</param>
    /// <param name="messageService">站内信领域服务（站内信托底：定向用户的通知站内信必达）.</param>
    /// <param name="dispatcher">渠道发送调度器.</param>
    /// <param name="templateApplier">通知模板应用器（入队前渲染，队列中的消息不依赖模板存续）.</param>
    public NotificationService(DatabaseContext databaseContext, IBackgroundJobClient backgroundJobClient, IMessageService messageService, NotificationChannelDispatcher dispatcher, NotificationTemplateApplier templateApplier)
    {
        _databaseContext = databaseContext;
        _backgroundJobClient = backgroundJobClient;
        _messageService = messageService;
        _dispatcher = dispatcher;
        _templateApplier = templateApplier;
    }

    /// <inheritdoc/>
    public async Task<int> NotifyAsync(NotificationMessage message, NotificationSendOptions? options = null, CancellationToken cancellationToken = default)
    {
        message = await _templateApplier.ApplyAsync(message, cancellationToken);
        await SendInSiteAsync(message, cancellationToken);
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
        message = await _templateApplier.ApplyAsync(message, cancellationToken);
        await SendInSiteAsync(message, cancellationToken);
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

    /// <summary>
    /// 同步向单个渠道发送（站内信托底语义与 <see cref="SendNowAsync"/> 一致），供渠道试发使用：
    /// 渠道已入库（Id>0）时落发送记录留痕；保存前试发的临时渠道（Id=0）不落记录.
    /// 不校验渠道启用状态，已保存渠道按实体直接发送.
    /// </summary>
    /// <param name="channel">目标渠道实体.</param>
    /// <param name="message">通知内容.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回该渠道的发送结果.</returns>
    public async Task<NotificationChannelSendResult> SendToChannelAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken = default)
    {
        message = await _templateApplier.ApplyAsync(message, cancellationToken);
        await SendInSiteAsync(message, cancellationToken);

        var (success, error, durationMs) = await _dispatcher.SendAsync(channel, message, cancellationToken);
        if (channel.Id > 0)
        {
            var log = CreateLog(channel, message);
            log.Status = success ? (int)NotificationSendStatus.Success : (int)NotificationSendStatus.Failed;
            log.Error = NotificationText.Truncate(error, 1000);
            log.DurationMs = durationMs;
            _databaseContext.NotificationLogs.Add(log);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return new NotificationChannelSendResult
        {
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            Success = success,
            Error = error,
            DurationMs = durationMs
        };
    }

    /// <summary>
    /// 站内信托底：TargetUserIds 定向到具体站内用户时站内信必达，先于外部渠道且与渠道解析/投递结果解耦；
    /// 无目标的全员/广播型通知不自动发站内信，由调用方自行决定（如广播的 sendInSite 选项）.
    /// </summary>
    private async Task SendInSiteAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (message.TargetUserIds is not { Count: > 0 })
        {
            return;
        }

        await _messageService.SendToUsersAsync(NotificationInSiteMessage.Create(message), message.TargetUserIds, cancellationToken);
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
            TargetUserIds = NotificationText.Truncate(string.Join(',', message.TargetUserIds ?? []), 1000),
            Recipients = ResolveRecipientsSnapshot(channel, message),
            Status = (int)NotificationSendStatus.Pending,
            Error = string.Empty
        };
    }

    /// <summary>
    /// 按渠道类型快照本次实际投递的收件人（SMTP 记邮箱、短信记手机号；固定地址渠道留空）.
    /// </summary>
    private static string ResolveRecipientsSnapshot(NotificationChannelEntity channel, NotificationMessage message)
    {
        return (NotificationChannelType)channel.Type switch
        {
            NotificationChannelType.Smtp => NotificationText.Truncate(string.Join(',', message.EmailRecipients ?? []), 1000),
            NotificationChannelType.AliyunSms or NotificationChannelType.TencentSms => NotificationText.Truncate(string.Join(',', message.PhoneNumbers ?? []), 1000),
            _ => string.Empty
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
