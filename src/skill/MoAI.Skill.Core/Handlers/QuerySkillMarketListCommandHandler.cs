using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySkillMarketListCommand"/>
/// </summary>
public class QuerySkillMarketListCommandHandler : IRequestHandler<QuerySkillMarketListCommand, QuerySkillListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySkillMarketListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QuerySkillMarketListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QuerySkillListCommandResponse> Handle(QuerySkillMarketListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Skills
            .AsNoTracking()
            .Where(x => x.IsPublic);

        query = QuerySkillListHelper.WhereKeywords(query, request.Keywords);
        query = QuerySkillListHelper.WhereClassify(query, request.ClassifyId);

        var entities = await query
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken);

        var items = await QuerySkillListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QuerySkillListCommandResponse { Items = items };
    }
}
