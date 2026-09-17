using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMySkillListCommand"/>
/// </summary>
public class QueryMySkillListCommandHandler : IRequestHandler<QueryMySkillListCommand, QuerySkillListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMySkillListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryMySkillListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillListCommandResponse> Handle(QueryMySkillListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Skills
            .AsNoTracking()
            .Where(x => x.TeamId == 0 && !x.IsSystem && x.CreateUserId == request.ContextUserId);

        query = QuerySkillListHelper.WhereKeywords(query, request.Keywords);

        var entities = await query
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var items = await QuerySkillListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QuerySkillListCommandResponse { Items = items };
    }
}
