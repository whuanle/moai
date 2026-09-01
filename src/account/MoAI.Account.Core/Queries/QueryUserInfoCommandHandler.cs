using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Queries;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.Account.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserInfoCommand"/>
/// </summary>
public class QueryUserInfoCommandHandler : IRequestHandler<QueryUserInfoCommand, Queries.Responses.UserStateInfo>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    public QueryUserInfoCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<Queries.Responses.UserStateInfo> Handle(QueryUserInfoCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.Users
            .AnyAsync(u => u.Id == request.UserId && u.IsDeleted == 0, cancellationToken);

        if (!exist)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        return await _userAccountService.GetUserStateAsync(request.UserId, cancellationToken);
    }
}
