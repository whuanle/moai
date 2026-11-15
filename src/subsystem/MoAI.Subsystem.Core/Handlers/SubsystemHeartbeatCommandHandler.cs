using MediatR;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Commands.Responses;
using MoAI.Subsystem.Core.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="SubsystemHeartbeatCommand"/>
/// </summary>
public class SubsystemHeartbeatCommandHandler : IRequestHandler<SubsystemHeartbeatCommand, SubsystemHeartbeatCommandResponse>
{
    private readonly ISubsystemOnlineService _onlineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemHeartbeatCommandHandler"/> class.
    /// </summary>
    /// <param name="onlineService">在线在场服务.</param>
    public SubsystemHeartbeatCommandHandler(ISubsystemOnlineService onlineService)
    {
        _onlineService = onlineService;
    }

    /// <inheritdoc/>
    public async Task<SubsystemHeartbeatCommandResponse> Handle(SubsystemHeartbeatCommand request, CancellationToken cancellationToken)
    {
        await _onlineService.TouchAsync(request.SubsystemId, request.InstanceId, cancellationToken);

        // 续期后立即回读在线实例数（读脚本会顺带清理窗口外实例），接入方无需再单独查 info。
        var onlineInstances = await _onlineService.GetOnlineInstanceCountAsync(request.SubsystemId);

        return new SubsystemHeartbeatCommandResponse
        {
            ServerTime = DateTimeOffset.UtcNow,
            OnlineInstances = onlineInstances,
        };
    }
}
