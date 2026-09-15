using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryPromptMarketListCommand"/>
/// </summary>
public class QueryPromptMarketListCommandHandler : IRequestHandler<QueryPromptMarketListCommand, QueryPromptListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptMarketListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryPromptMarketListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryPromptListCommandResponse> Handle(QueryPromptMarketListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Prompts
            .AsNoTracking()
            .Where(x => x.IsPublic);

        query = QueryPromptListHelper.WhereKeywords(query, request.Keywords);

        if (request.PromptClassId != null)
        {
            query = query.Where(x => x.PromptClassId == request.PromptClassId);
        }

        var entities = await query
            .OrderByDescending(x => x.Counter)
            .ThenByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var items = await QueryPromptListHelper.MapAsync(entities, _databaseContext, _userInfoFillService, cancellationToken);

        return new QueryPromptListCommandResponse { Items = items };
    }
}
