using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
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
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllOAuthConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryAllOAuthConnectionCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryAllOAuthConnectionCommandResponse> Handle(QueryAllOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.OauthConnections
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

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryAllOAuthConnectionCommandResponse { Items = items };
    }
}
