using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.TeamAgent.Queries;
using MoAI.TeamAgent.Queries.Responses;
using MoAI.TeamAgent.Services;

namespace MoAI.TeamAgent.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamAgentDetailCommand"/>
/// </summary>
public class QueryTeamAgentDetailCommandHandler : IRequestHandler<QueryTeamAgentDetailCommand, QueryTeamAgentDetailCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAgentDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryTeamAgentDetailCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAgentDetailCommandResponse> Handle(QueryTeamAgentDetailCommand request, CancellationToken cancellationToken)
    {
        var agent = await _databaseContext.TeamAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AgentId, cancellationToken)
            ?? throw new BusinessException("智能体不存在.") { StatusCode = 404 };

        await TeamAgentAccessGuard.EnsureTeamMemberAsync(agent.TeamId, request.ContextUserId, _teamService, cancellationToken);

        var item = new TeamAgentDetailItem
        {
            Id = agent.Id,
            TeamId = agent.TeamId,
            Key = agent.Key,
            Name = agent.Name,
            Description = agent.Description,
            AvatarObjectKey = agent.AvatarObjectKey ?? string.Empty,
            Enabled = agent.Enabled,
            IsSystem = agent.IsSystem,
            MainPrompt = agent.MainPrompt,
            MainPluginIds = TeamAgentJson.ParseGuidList(agent.MainPluginIds),
            MainWikiIds = TeamAgentJson.ParseLongList(agent.MainWikiIds),
            MainGraphIds = TeamAgentJson.ParseLongList(agent.MainGraphIds),
            Subs = TeamAgentJson.ParseSubs(agent.Subs),
            CreateUserId = (int)agent.CreateUserId,
            CreateTime = agent.CreateTime,
            UpdateUserId = (int)agent.UpdateUserId,
            UpdateTime = agent.UpdateTime,
        };

        await _userInfoFillService.FillAsync([item], cancellationToken);

        return new QueryTeamAgentDetailCommandResponse
        {
            Agent = item,
        };
    }
}
