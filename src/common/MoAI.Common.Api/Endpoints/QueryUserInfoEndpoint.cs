using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Models;
using MoAI.Login.Queries.Responses;
namespace MoAI.Common.Endpoints;

/// <summary>
/// 查询用户基本信息.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/userinfo")]
public class QueryUserInfoEndpoint : EndpointWithoutRequest<UserStateInfo>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserInfoEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserInfoEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override Task<UserStateInfo> ExecuteAsync(CancellationToken ct)
    {
        return _mediator.Send(new QueryUserViewUserInfoCommand(), ct);
    }
}
