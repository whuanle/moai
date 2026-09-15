using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserPromptListCommand"/>
/// </summary>
public class QueryUserPromptListCommandHandler : IRequestHandler<QueryUserPromptListCommand, QueryPromptListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserPromptListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryUserPromptListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryPromptListCommandResponse> Handle(QueryUserPromptListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Prompts
            .AsNoTracking()
            .Where(x => x.TeamId == 0 && x.CreateUserId == request.ContextUserId);

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
