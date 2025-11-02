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
         四种情况：
         1，系统知识库
         2，自己创建的
         3，是知识库成员
         4，无关，但是公开的知识库
         */

        var query = _databaseContext.Wikis.AsQueryable();

        if (request.QueryType == WikiQueryType.System)
        {
            query = query.Where(x => x.IsSystem);
        }
        else
        {
            query = query.Where(x => !x.IsSystem);
            if (request.QueryType == WikiQueryType.Own)
            {
                query = query.Where(x => x.CreateUserId == request.ContextUserId);
            }
            else if (request.QueryType == WikiQueryType.User)
            {
                query = query.Where(x => _databaseContext.WikiUsers.Any(a => a.UserId == request.ContextUserId && a.WikiId == x.Id));
            }
        }

        if (request.IsPublic == true)
        {
            query = query.Where(x => x.IsPublic);
        }

        var response = await query
            .OrderBy(x => x.Name)
            .Select(x => new QueryWikiInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                IsSystem = x.IsSystem,
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

        if (response.Where(x => x.IsSystem).Any())
        {
            var adminIds = await _mediator.Send(new QueryAdminIdsCommand());
            foreach (var item in response.Where(x => x.IsSystem))
            {
                item.SetProperty(x => x.IsUser, adminIds.AdminIds.Contains(request.ContextUserId));
            }
        }

        await _mediator.Send(new FillUserInfoCommand { Items = response });

        return response;
    }
}
