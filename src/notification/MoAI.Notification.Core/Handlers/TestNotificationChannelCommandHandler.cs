using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;
using MoAI.Notification.Services;
using MoAI.Notification.Senders;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="TestNotificationChannelCommand"/>.
/// </summary>
public class TestNotificationChannelCommandHandler : IRequestHandler<TestNotificationChannelCommand, TestNotificationChannelCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly NotificationService _notificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestNotificationChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="notificationService">通知中心领域服务（试发走领域服务：落发送记录 + 站内信托底，试发人收到站内信副本）.</param>
    public TestNotificationChannelCommandHandler(DatabaseContext databaseContext, NotificationService notificationService)
    {
        _databaseContext = databaseContext;
        _notificationService = notificationService;
    }

    /// <inheritdoc/>
    public async Task<TestNotificationChannelCommandResponse> Handle(TestNotificationChannelCommand request, CancellationToken cancellationToken)
    {
        NotificationChannelEntity channel;
        if (request.Id > 0)
        {
            channel = await _databaseContext.NotificationChannels.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new BusinessException("通知渠道不存在") { StatusCode = 404 };
        }
        else
        {
            // 保存前试发：使用传入的类型与配置构造临时渠道
            channel = new NotificationChannelEntity
            {
                Id = 0,
                Name = string.Empty,
                Type = (int)request.Type!.GetValueOrDefault(),
                Config = request.Config!,
                Enabled = true,
                Description = string.Empty
            };
        }

        var message = new NotificationMessage
        {
            Title = $"通知渠道测试 - {(channel.Name.Length == 0 ? "未保存配置" : channel.Name)}",
            Content = $"这是一条 MoAI 通知中心的测试通知，发送时间 {NotificationText.Time()}。收到即表示渠道配置可用。",
            Level = NotificationLevel.Info,
            Source = "notification",
            TargetUserIds = new List<long> { request.ContextUserId },
            EmailRecipients = channel.Type == (int)NotificationChannelType.Smtp ? request.Recipients : null,
            PhoneNumbers = channel.Type is (int)NotificationChannelType.AliyunSms or (int)NotificationChannelType.TencentSms ? request.Recipients : null
        };

        var result = await _notificationService.SendToChannelAsync(channel, message, cancellationToken);
        return new TestNotificationChannelCommandResponse
        {
            Success = result.Success,
            Error = result.Error,
            DurationMs = result.DurationMs
        };
    }
}
