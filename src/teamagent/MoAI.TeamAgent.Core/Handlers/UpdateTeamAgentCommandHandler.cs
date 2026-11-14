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
/// <inheritdoc cref="UpdateTeamAgentCommand"/>
/// </summary>
public class UpdateTeamAgentCommandHandler : IRequestHandler<UpdateTeamAgentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamAgentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateTeamAgentCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamAgentCommand request, CancellationToken cancellationToken)
    {
        var agent = await _databaseContext.TeamAgents
            .FirstOrDefaultAsync(x => x.Id == request.AgentId, cancellationToken)
            ?? throw new BusinessException("智能体不存在.") { StatusCode = 404 };

        await TeamAgentAccessGuard.EnsureCanManageAsync(agent.TeamId, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

        var nameExist = await _databaseContext.TeamAgents
            .AnyAsync(x => x.TeamId == agent.TeamId && x.Name == request.Name && x.Id != agent.Id, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("智能体名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var (mainPluginIds, mainWikiIds, mainGraphIds, subs) = await TeamAgentCommandHelper.NormalizeAsync(
            _databaseContext, agent.TeamId, request.MainPluginIds, request.MainWikiIds, request.MainGraphIds, request.Subs, cancellationToken);

        // 标识 key 创建后不可变更，不在更新字段内
        agent.Name = request.Name;
        agent.Description = request.Description ?? string.Empty;
        agent.Enabled = request.Enabled;
        agent.MainPrompt = request.MainPrompt;
        agent.MainPluginIds = TeamAgentJson.SerializePluginIds(mainPluginIds);
        agent.MainWikiIds = TeamAgentJson.SerializeWikiIds(mainWikiIds);
        agent.MainGraphIds = TeamAgentJson.SerializeWikiIds(mainGraphIds);
        agent.Subs = TeamAgentJson.SerializeSubs(subs);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
