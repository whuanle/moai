using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.OauthConnect.Queries;
using MoAI.OauthConnect.Queries.Responses;

namespace MoAI.OauthConnect.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAllOAuthConnectionCommand"/>
/// </summary>
public class QueryAllOAuthConnectionCommandHandler : IRequestHandler<QueryAllOAuthConnectionCommand, QueryAllOAuthConnectionCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllOAuthConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAllOAuthConnectionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAllOAuthConnectionCommandResponse> Handle(QueryAllOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.OauthConnections
            .Where(x => x.IsDeleted == 0)
            .Select(x => new QueryAllOAuthConnectionCommandResponseItem
            {
                Id = x.Id,
                Name = x.Name,
                IconUrl = x.IconUrl,
                Provider = x.Provider,
                Key = x.Key,
                WellKnown = x.WellKnown,
                AuthorizeUrl = x.AuthorizeUrl,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
            })
            .ToListAsync(cancellationToken);

        return new QueryAllOAuthConnectionCommandResponse { Items = items };
    }
}
