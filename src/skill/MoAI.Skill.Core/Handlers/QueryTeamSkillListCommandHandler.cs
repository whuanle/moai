using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamSkillListCommand"/>
/// </summary>
public class QueryTeamSkillListCommandHandler : IRequestHandler<QueryTeamSkillListCommand, QuerySkillListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamSkillListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryTeamSkillListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillListCommandResponse> Handle(QueryTeamSkillListCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.Skills
            .AsNoTracking()
            .Where(x => x.TeamId == request.TeamId);

        query = QuerySkillListHelper.WhereKeywords(query, request.Keywords);

        var entities = await query
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var items = await QuerySkillListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QuerySkillListCommandResponse { Items = items };
    }
}
