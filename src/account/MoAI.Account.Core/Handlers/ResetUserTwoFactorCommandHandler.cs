using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="ResetUserTwoFactorCommand"/>.
/// </summary>
public class ResetUserTwoFactorCommandHandler : IRequestHandler<ResetUserTwoFactorCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetUserTwoFactorCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    public ResetUserTwoFactorCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ResetUserTwoFactorCommand request, CancellationToken cancellationToken)
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

        // 目标保护规则与重置密码一致：不能动 root，管理员不能动其他管理员
        if (user.Id.ToString() == rootValue)
        {
            throw new BusinessException("不能重置超级管理员的两步验证.") { StatusCode = 400 };
        }

        if (user.IsAdmin && request.ContextUserId.ToString() != rootValue)
        {
            throw new BusinessException("管理员不能重置其他管理员的两步验证.") { StatusCode = 403 };
        }

        if (!user.IsTotpEnabled && string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            throw new BusinessException("该用户尚未绑定两步验证.") { StatusCode = 400 };
        }

        user.IsTotpEnabled = false;
        user.TotpSecret = null;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
