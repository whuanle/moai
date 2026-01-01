using MediatR;
using MoAI.Infra.Models;
using MoAI.Login.Queries;
using MoAI.Login.Queries.Responses;

namespace MoAI.Common.Queries;

/// <summary>
/// 处理查询用户信息的命令.
/// </summary>
public class QueryUserViewUserInfoCommandHandler : IRequestHandler<QueryUserViewUserInfoCommand, UserStateInfo>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserViewUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public QueryUserViewUserInfoCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> Handle(QueryUserViewUserInfoCommand request, CancellationToken cancellationToken)
    {
        var queryResult = await _mediator.Send(new QueryUserStateCommand { UserId = request.ContextUserId });
        return queryResult;
    }
}
