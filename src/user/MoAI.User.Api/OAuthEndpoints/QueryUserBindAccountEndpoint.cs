using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.User.Queries;
using MoAI.User.Queries.Responses;

namespace MoAI.User.OAuthEndpoints;

/// <summary>
/// 查询用户已经绑定的第三方账号.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/oauth_list")]
public class QueryUserBindAccountEndpoint : EndpointWithoutRequest<QueryUserBindAccountCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserBindAccountEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserBindAccountEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryUserBindAccountCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(
            new QueryUserBindAccountCommand
            {
                UserId = _userContext.UserId
            },
            ct);
    }
}