using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Account.Services;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateUserIsAdminCommand"/>
/// </summary>
public class UpdateUserIsAdminCommandHandler : IRequestHandler<UpdateUserIsAdminCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserIsAdminCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    public UpdateUserIsAdminCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserIsAdminCommand request, CancellationToken cancellationToken)
    {
        var user = await GetUserOrDefaultAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        var rootValue = await GetRootValueAsync(cancellationToken);
        if (user.Id.ToString() == rootValue)
        {
            throw new BusinessException("不能修改超级管理员账号.") { StatusCode = 400 };
        }

        if (user.Id == request.ContextUserId)
        {
            throw new BusinessException("不能修改自己的管理员角色.") { StatusCode = 400 };
        }

        user.IsAdmin = request.IsAdmin;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task<Database.Entities.UserEntity?> GetUserOrDefaultAsync(long userId, CancellationToken cancellationToken)
    {
        return await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsDeleted == 0, cancellationToken);
    }

    private async Task<string> GetRootValueAsync(CancellationToken cancellationToken)
    {
        return await _databaseContext.Settings
            .Where(s => s.Key == "root")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
    }
}
