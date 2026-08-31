using MediatR;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Account.Services;

namespace MoAI.Account.Queries;

/// <summary>
/// 处理查询用户信息的命令.
/// </summary>
public class QueryUserViewUserInfoCommandHandler : IRequestHandler<QueryUserViewUserInfoCommand, UserStateInfo>
{
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserViewUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="userAccountService"></param>
    public QueryUserViewUserInfoCommandHandler(IUserAccountService userAccountService)
    {
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> Handle(QueryUserViewUserInfoCommand request, CancellationToken cancellationToken)
    {
        return await _userAccountService.GetUserStateAsync(request.ContextUserId, cancellationToken);
    }
}
