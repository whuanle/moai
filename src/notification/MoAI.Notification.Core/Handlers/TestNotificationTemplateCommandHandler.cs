using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;
using MoAI.Notification.Services;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="TestNotificationTemplateCommand"/>.
/// </summary>
public class TestNotificationTemplateCommandHandler : IRequestHandler<TestNotificationTemplateCommand, TestNotificationTemplateCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly NotificationTemplateApplier _templateApplier;
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestNotificationTemplateCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文（解析试发渠道类型以分发动态收件人）.</param>
    /// <param name="templateApplier">通知模板应用器.</param>
    /// <param name="notificationService">通知中心领域服务（同步试发并留发送记录）.</param>
    public TestNotificationTemplateCommandHandler(DatabaseContext databaseContext, NotificationTemplateApplier templateApplier, INotificationService notificationService)
    {
        _databaseContext = databaseContext;
        _templateApplier = templateApplier;
        _notificationService = notificationService;
    }

    /// <inheritdoc/>
    public async Task<TestNotificationTemplateCommandResponse> Handle(TestNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        // 按试发渠道类型分发动态收件人：SMTP 记邮箱、短信记手机号，其它渠道忽略
        var toEmail = false;
        var toPhone = false;
        if (request.ChannelId is > 0)
        {
            var channelType = await _databaseContext.NotificationChannels.AsNoTracking()
                .Where(x => x.Id == request.ChannelId.Value)
                .Select(x => (int)x.Type)
                .FirstOrDefaultAsync(cancellationToken);
            toEmail = channelType == (int)NotificationChannelType.Smtp;
            toPhone = channelType is (int)NotificationChannelType.AliyunSms or (int)NotificationChannelType.TencentSms;
        }

        var message = await _templateApplier.ApplyAsync(new NotificationMessage
        {
            Title = "通知模板测试",
            Content = string.Empty,
            Level = NotificationLevel.Info,
            Source = "notification",
            TemplateId = request.TemplateId,
            TemplateVariables = request.Variables,
            EmailRecipients = toEmail ? request.Recipients : null,
            PhoneNumbers = toPhone ? request.Recipients : null
        }, cancellationToken);

        if (request.ChannelId is not > 0)
        {
            // 仅渲染预览，不发送
            return new TestNotificationTemplateCommandResponse
            {
                RenderedTitle = message.Title,
                RenderedContent = message.Content
            };
        }

        var results = await _notificationService.SendNowAsync(message, new NotificationSendOptions
        {
            ChannelIds = new List<long> { request.ChannelId.Value }
        }, cancellationToken);

        var result = results.FirstOrDefault()
            ?? throw new BusinessException("试发渠道不存在或未启用") { StatusCode = 404 };

        return new TestNotificationTemplateCommandResponse
        {
            RenderedTitle = message.Title,
            RenderedContent = message.Content,
            Success = result.Success,
            Error = result.Error,
            DurationMs = result.DurationMs
        };
    }
}
