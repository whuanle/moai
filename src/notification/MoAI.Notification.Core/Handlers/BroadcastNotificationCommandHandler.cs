using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.Notification.Commands;
using MoAI.Notification.Models;
using MoAI.Notification.Services;

namespace MoAI.Notification.Handlers;

/// <summary>
/// <inheritdoc cref="BroadcastNotificationCommand"/>.
/// </summary>
public class BroadcastNotificationCommandHandler : IRequestHandler<BroadcastNotificationCommand, BroadcastNotificationCommandResponse>
{
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastNotificationCommandHandler"/> class.
    /// </summary>
    /// <param name="notificationService">通知中心领域服务.</param>
    public BroadcastNotificationCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <inheritdoc/>
    public async Task<BroadcastNotificationCommandResponse> Handle(BroadcastNotificationCommand request, CancellationToken cancellationToken)
    {
        var channelCount = await _notificationService.NotifyAsync(new NotificationMessage
        {
            Title = request.Title,
            Content = request.Content,
            Level = request.Level,
            Source = "notification"
        }, null, cancellationToken);

        return new BroadcastNotificationCommandResponse { ChannelCount = channelCount };
    }
}
