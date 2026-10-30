using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="SubsystemHeartbeatCommand"/>
/// </summary>
public class SubsystemHeartbeatCommandHandler : IRequestHandler<SubsystemHeartbeatCommand, EmptyCommandResponse>
{
    private readonly SubsystemOnlineService _onlineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemHeartbeatCommandHandler"/> class.
    /// </summary>
    /// <param name="onlineService">在线在场服务.</param>
    public SubsystemHeartbeatCommandHandler(SubsystemOnlineService onlineService)
    {
        _onlineService = onlineService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SubsystemHeartbeatCommand request, CancellationToken cancellationToken)
    {
        await _onlineService.TouchAsync(request.SubsystemId, request.InstanceId, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
