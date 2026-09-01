using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Queries.Responses;
using MoAI.Database;

namespace MoAI.Account.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserBoundAccountsCommand"/>
/// </summary>
public class QueryUserBoundAccountsCommandHandler : IRequestHandler<QueryUserBoundAccountsCommand, QueryUserBoundAccountsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserBoundAccountsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryUserBoundAccountsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryUserBoundAccountsCommandResponse> Handle(QueryUserBoundAccountsCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.UserOauthConnections
            .Where(x => x.UserId == request.ContextUserId)
            .Join(
                _databaseContext.OauthConnections,
                x => x.ProviderId,
                o => o.Id,
                (x, o) => new BoundAccountInfo
                {
                    OAuthId = o.Id,
                    Name = o.Name,
                    Provider = o.Provider,
                    IconUrl = o.IconUrl,
                    CreateTime = x.CreateTime,
                })
            .ToListAsync(cancellationToken);

        return new QueryUserBoundAccountsCommandResponse { Items = items };
    }
}
