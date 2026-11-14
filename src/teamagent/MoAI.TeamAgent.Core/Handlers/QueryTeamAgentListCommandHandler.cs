using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Team.Services;
using MoAI.TeamAgent.Queries;
using MoAI.TeamAgent.Queries.Responses;
using MoAI.TeamAgent.Services;

namespace MoAI.TeamAgent.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamAgentListCommand"/>
/// </summary>
public class QueryTeamAgentListCommandHandler : IRequestHandler<QueryTeamAgentListCommand, QueryTeamAgentListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAgentListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryTeamAgentListCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAgentListCommandResponse> Handle(QueryTeamAgentListCommand request, CancellationToken cancellationToken)
    {
        var role = await TeamAgentAccessGuard.EnsureTeamMemberAsync(request.TeamId, request.ContextUserId, _teamService, cancellationToken);

        var query = _databaseContext.TeamAgents.AsNoTracking().Where(x => x.TeamId == request.TeamId);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var keyword = request.SearchText.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.Description.Contains(keyword));
        }

        var agents = await query
            .OrderByDescending(x => x.IsSystem)
            .ThenByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var items = agents.Select(x => new TeamAgentListItem
        {
            Id = x.Id,
            Key = x.Key,
            Name = x.Name,
            Description = x.Description,
            AvatarObjectKey = x.AvatarObjectKey ?? string.Empty,
            Enabled = x.Enabled,
            IsSystem = x.IsSystem,
            SubCount = TeamAgentJson.ParseSubs(x.Subs).Count,
            CreateUserId = (int)x.CreateUserId,
            CreateTime = x.CreateTime,
            UpdateUserId = (int)x.UpdateUserId,
            UpdateTime = x.UpdateTime,
        }).ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryTeamAgentListCommandResponse
        {
            MyRole = (int)role,
            Items = items,
        };
    }
}
