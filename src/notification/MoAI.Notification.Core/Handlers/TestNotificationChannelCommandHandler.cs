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
    private readonly NotificationChannelDispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestNotificationChannelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="dispatcher">渠道发送调度器.</param>
    public TestNotificationChannelCommandHandler(DatabaseContext databaseContext, NotificationChannelDispatcher dispatcher)
    {
        _databaseContext = databaseContext;
        _dispatcher = dispatcher;
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
            Source = "notification"
        };

        var (success, error, durationMs) = await _dispatcher.SendAsync(channel, message, cancellationToken);
        return new TestNotificationChannelCommandResponse
        {
            Success = success,
            Error = error,
            DurationMs = durationMs
        };
    }
}
