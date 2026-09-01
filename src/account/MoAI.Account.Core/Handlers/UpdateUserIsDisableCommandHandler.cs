using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Account.Services;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateUserIsDisableCommand"/>
/// </summary>
public class UpdateUserIsDisableCommandHandler : IRequestHandler<UpdateUserIsDisableCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserIsDisableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    public UpdateUserIsDisableCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserIsDisableCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsDeleted == 0, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        var rootValue = await _databaseContext.Settings
            .Where(s => s.Key == "root")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        if (user.Id.ToString() == rootValue)
        {
            throw new BusinessException("不能禁用超级管理员账号.") { StatusCode = 400 };
        }

        if (user.Id == request.ContextUserId)
        {
            throw new BusinessException("不能禁用自己的账号.") { StatusCode = 400 };
        }

        if (user.IsAdmin && request.ContextUserId.ToString() != rootValue)
        {
            throw new BusinessException("管理员不能操作其他管理员账号.") { StatusCode = 403 };
        }

        user.IsDisable = request.IsDisable;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
