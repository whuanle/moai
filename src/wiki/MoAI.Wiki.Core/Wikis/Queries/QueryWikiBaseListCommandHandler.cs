using LinqKit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Storage.Queries;
using MoAI.Team.Models;
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
         1，私有知识库
         2，团队的知识库
         */

        // 分开查方便一些
        var query = _databaseContext.Wikis.AsQueryable();
        List<QueryWikiInfoResponse> responses = default!;

        if (request.IsOwn == true)
        {
            query = query.Where(x => x.TeamId == 0 && x.CreateUserId == request.ContextUserId);
            responses = await query.DynamicOrder(request.OrderByFields)
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
                    Counter = x.Counter,
                    TeamId = x.TeamId,
                    AvatarKey = x.Avatar,
                    Role = TeamRole.Owner,
                    DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
                })
                .ToListAsync(cancellationToken);
        }
        else if (request.IsInTeam == true)
        {
            var predicate = PredicateBuilder.New<WikiEntity>(true);
            predicate = predicate.And(x => x.TeamId != 0);
            predicate = predicate.And(x => _databaseContext.TeamUsers.Where(a => a.UserId == request.ContextUserId && x.TeamId == a.TeamId).Any());
            responses = await query.Where(predicate).DynamicOrder(request.OrderByFields)
                .LeftJoin(_databaseContext.Teams, a => a.TeamId, b => b.Id, (x, y) => new QueryWikiInfoResponse
                {
                    WikiId = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    IsPublic = x.IsPublic,
                    CreateUserId = x.CreateUserId,
                    UpdateUserId = x.UpdateUserId,
                    CreateTime = x.CreateTime,
                    UpdateTime = x.UpdateTime,
                    TeamId = x.TeamId,
                    AvatarKey = x.Avatar,
                    Counter = x.Counter,
                    TeamName = y.Name,
                    Role = TeamRole.Collaborator,
                    DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            var predicate = PredicateBuilder.New<WikiEntity>(true);
            predicate = predicate.Or(x => x.TeamId == 0 && x.CreateUserId == request.ContextUserId);
            predicate = predicate.Or(x => x.TeamId != 0 && _databaseContext.TeamUsers.Where(a => a.UserId == request.ContextUserId && x.TeamId == a.TeamId).Any());

            responses = await query.Where(predicate).DynamicOrder(request.OrderByFields)
                .LeftJoin(_databaseContext.Teams, a => a.TeamId, b => b.Id, (x, y) => new QueryWikiInfoResponse
                {
                    WikiId = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    IsPublic = x.IsPublic,
                    Counter = x.Counter,
                    CreateUserId = x.CreateUserId,
                    UpdateUserId = x.UpdateUserId,
                    CreateTime = x.CreateTime,
                    UpdateTime = x.UpdateTime,
                    TeamId = x.TeamId,
                    AvatarKey = x.Avatar,
                    TeamName = y.Name,
                    Role = TeamRole.Collaborator,
                    DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
                })
                .ToListAsync(cancellationToken);
        }

        await _mediator.Send(new QueryAvatarUrlCommand { Items = responses });
        await _mediator.Send(new FillUserInfoCommand { Items = responses });

        return responses;
    }
}
