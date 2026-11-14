using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Message.Services;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;
using MoAI.Notification.Services;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="BroadcastNotificationCommand"/>.
/// </summary>
public class BroadcastNotificationCommandHandler : IRequestHandler<BroadcastNotificationCommand, BroadcastNotificationCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly INotificationService _notificationService;
    private readonly IMessageService _messageService;
    private readonly NotificationTemplateApplier _templateApplier;

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastNotificationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文（解析投递渠道类型以校验动态收件人）.</param>
    /// <param name="notificationService">通知中心领域服务.</param>
    /// <param name="messageService">站内信领域服务（站内信选项直接全员下发，无需渠道配置）.</param>
    /// <param name="templateApplier">通知模板应用器（先渲染再投递，站内信同样使用渲染后文本）.</param>
    public BroadcastNotificationCommandHandler(DatabaseContext databaseContext, INotificationService notificationService, IMessageService messageService, NotificationTemplateApplier templateApplier)
    {
        _databaseContext = databaseContext;
        _notificationService = notificationService;
        _messageService = messageService;
        _templateApplier = templateApplier;
    }

    /// <inheritdoc/>
    public async Task<BroadcastNotificationCommandResponse> Handle(BroadcastNotificationCommand request, CancellationToken cancellationToken)
    {
        await ValidateRecipientsAsync(request, cancellationToken);

        var message = await _templateApplier.ApplyAsync(new NotificationMessage
        {
            Title = request.Title,
            Content = request.Content,
            Level = request.Level,
            Source = "notification",
            TemplateId = request.TemplateId,
            TemplateVariables = request.TemplateVariables,
            EmailRecipients = request.EmailRecipients,
            PhoneNumbers = request.PhoneNumbers
        }, cancellationToken);

        var channelCount = 0;
        if (request.AllChannels)
        {
            channelCount = await _notificationService.NotifyAsync(message, null, cancellationToken);
        }
        else if (request.ChannelIds is { Count: > 0 })
        {
            channelCount = await _notificationService.NotifyAsync(message, new NotificationSendOptions { ChannelIds = request.ChannelIds }, cancellationToken);
        }

        if (request.SendInSite)
        {
            // 站内信以纯文本展示：HTML 模板降级去标签（与站内信托底共用同一构造规则）
            await _messageService.SendToAllUsersAsync(NotificationInSiteMessage.Create(message), cancellationToken);
        }

        return new BroadcastNotificationCommandResponse { ChannelCount = channelCount };
    }

    /// <summary>
    /// 动态收件人校验：投递渠道含 SMTP 时邮箱收件人必填，含短信渠道时手机号必填（格式已由命令静态校验）.
    /// </summary>
    private async Task ValidateRecipientsAsync(BroadcastNotificationCommand request, CancellationToken cancellationToken)
    {
        var hasChannelTarget = request.AllChannels || request.ChannelIds is { Count: > 0 };
        if (!hasChannelTarget)
        {
            return;
        }

        var query = _databaseContext.NotificationChannels.AsNoTracking().Where(x => x.Enabled);
        if (request.ChannelIds is { Count: > 0 })
        {
            var channelIds = request.ChannelIds.ToList();
            query = query.Where(x => channelIds.Contains(x.Id));
        }

        var channelTypes = await query.Select(x => (int)x.Type).ToListAsync(cancellationToken);
        if (channelTypes.Count == 0)
        {
            return;
        }

        if (channelTypes.Contains((int)NotificationChannelType.Smtp))
        {
            var error = NotificationChannelConfigs.CheckEmailRecipients(request.EmailRecipients);
            if (error != null)
            {
                throw new BusinessException($"投递渠道包含 SMTP 邮件渠道，{error}") { StatusCode = 400 };
            }
        }

        if (channelTypes.Contains((int)NotificationChannelType.AliyunSms) || channelTypes.Contains((int)NotificationChannelType.TencentSms))
        {
            var error = NotificationChannelConfigs.CheckPhoneRecipients(request.PhoneNumbers);
            if (error != null)
            {
                throw new BusinessException($"投递渠道包含短信渠道，{error}") { StatusCode = 400 };
            }
        }
    }
}
