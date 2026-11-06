using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.TeamAgent.Commands;
using MoAI.TeamAgent.Services;

namespace MoAI.TeamAgent.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamAgentAvatarCommand"/>
/// </summary>
public class UpdateTeamAgentAvatarCommandHandler : IRequestHandler<UpdateTeamAgentAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamAgentAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateTeamAgentAvatarCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamAgentAvatarCommand request, CancellationToken cancellationToken)
    {
        var agent = await _databaseContext.TeamAgents
            .FirstOrDefaultAsync(x => x.Id == request.AgentId, cancellationToken)
            ?? throw new BusinessException("智能体不存在.") { StatusCode = 404 };

        await TeamAgentAccessGuard.EnsureCanManageAsync(agent.TeamId, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

        await TeamAgentCommandHelper.EnsureAvatarValidAsync(_databaseContext, request.ObjectKey, cancellationToken);

        agent.AvatarObjectKey = request.ObjectKey;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
