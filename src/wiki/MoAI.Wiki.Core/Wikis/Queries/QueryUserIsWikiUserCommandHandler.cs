using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiUsersCommand"/>
/// </summary>
public class QueryUserIsWikiUserCommandHandler : IRequestHandler<QueryWikiUsersCommand, QueryWikiUsersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserIsWikiUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryUserIsWikiUserCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiUsersCommandResponse> Handle(QueryWikiUsersCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.WikiUsers
            .Where(x => x.WikiId == request.WikiId)
            .Join(_databaseContext.Users, wikiUser => wikiUser.UserId, user => user.Id, (wikiUser, user) => new QueryWikiUsersCommandResponseItem
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                NickName = user.NickName ?? string.Empty,
            })
            .ToListAsync(cancellationToken);

        return new QueryWikiUsersCommandResponse { Users = result };
    }
}
