using MediatR;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Account.Services;

namespace MoAI.Account.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserStateCommand"/>
/// </summary>
public class QueryUserStateCommandHandler : IRequestHandler<QueryUserStateCommand, UserStateInfo>
{
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserStateCommandHandler"/> class.
    /// </summary>
    /// <param name="userAccountService"></param>
    public QueryUserStateCommandHandler(IUserAccountService userAccountService)
    {
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> Handle(QueryUserStateCommand request, CancellationToken cancellationToken)
    {
        return await _userAccountService.GetUserStateAsync(request.UserId, cancellationToken);
    }
}
