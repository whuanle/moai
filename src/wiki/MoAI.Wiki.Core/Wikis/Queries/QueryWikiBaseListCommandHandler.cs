using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Helpers;
using MoAI.Login.Commands;
using MoAI.User.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiBaseListCommand"/>.
/// </summary>
public class QueryWikiBaseListCommandHandler : IRequestHandler<QueryWikiBaseListCommand, IReadOnlyCollection<QueryWikiInfoResponse>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiBaseListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueryWikiInfoResponse>> Handle(QueryWikiBaseListCommand request, CancellationToken cancellationToken)
    {
        /*
         三种情况：
         1，自己创建的
         2，是知识库成员
         3，无关，但是公开的知识库
         */

        var query = _databaseContext.Wikis.AsQueryable();

        if (request.QueryType == WikiQueryType.Own)
        {
            query = query.Where(x => x.CreateUserId == request.ContextUserId);
        }
        else if (request.QueryType == WikiQueryType.User)
        {
            query = query.Where(x => _databaseContext.WikiUsers.Any(a => a.UserId == request.ContextUserId && a.WikiId == x.Id));
        }
        else if (request.QueryType == WikiQueryType.Public)
        {
            query = query.Where(x => x.IsPublic);
        }
        else
        {
            query = query.Where(x => x.CreateUserId == request.ContextUserId || _databaseContext.WikiUsers.Any(a => a.UserId == request.ContextUserId && a.WikiId == x.Id) || x.IsPublic);
        }

        // 列表查询不返回所有字段.
        var response = await query
            .OrderBy(x => x.Name)
            .Select(x => new QueryWikiInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                IsUser = x.CreateUserId == request.ContextUserId || _databaseContext.WikiUsers.Any(a => a.UserId == request.ContextUserId),
                DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand { Items = response });

        return response;
    }
}
