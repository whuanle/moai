using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Prompt.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamPromptListCommand"/>
/// </summary>
public class QueryTeamPromptListCommandHandler : IRequestHandler<QueryTeamPromptListCommand, QueryPromptListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPromptListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryTeamPromptListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryPromptListCommandResponse> Handle(QueryTeamPromptListCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.Prompts
            .AsNoTracking()
            .Where(x => x.TeamId == request.TeamId);

        query = QueryPromptListHelper.WhereKeywords(query, request.Keywords);

        if (request.PromptClassId != null)
        {
            query = query.Where(x => x.PromptClassId == request.PromptClassId);
        }

        var entities = await query
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var items = await QueryPromptListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QueryPromptListCommandResponse { Items = items };
    }
}
