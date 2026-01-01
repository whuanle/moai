using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Storage.Queries;
using MoAI.Store.Queries;
using MoAI.Team.Models;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamListCommand"/>
/// </summary>
public class QueryTeamListQueryHandler : IRequestHandler<QueryTeamListCommand, QueryTeamListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamListQueryHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryTeamListQueryHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamListCommandResponse> Handle(QueryTeamListCommand request, CancellationToken cancellationToken)
    {
        var teamQuery = _databaseContext.Teams.AsQueryable();
        var userQuery = _databaseContext.TeamUsers.AsQueryable();
        if (request.IsOwn.HasValue && request.IsOwn.Value)
        {
            userQuery = userQuery.Where(x => x.UserId == request.ContextUserId && x.Role == (int)TeamRole.Owner);
        }
        else
        {
            userQuery = userQuery.Where(x => x.UserId == request.ContextUserId);
        }

        var results = await teamQuery.Join(userQuery, a => a.Id, b => b.TeamId, (a, b) => new QueryTeamListQueryResponseItem
        {
            Id = a.Id,
            Name = a.Name,
            Description = a.Description,
            AvatarKey = a.Avatar,
            CreateUserId = a.CreateUserId,
            CreateTime = a.CreateTime,
            Role = (TeamRole)b.Role,
            MemberCount = _databaseContext.TeamUsers.Count(tu => tu.TeamId == a.Id)
        }).ToArrayAsync();

        await _mediator.Send(new QueryAvatarUrlCommand { Items = results });
        return new QueryTeamListCommandResponse { Items = results };
    }
}
