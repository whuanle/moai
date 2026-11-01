using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Auth.Commands;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using MoAI.Infra.Services;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="DisableTwoFactorCommand"/>.
/// </summary>
public class DisableTwoFactorCommandHandler : IRequestHandler<DisableTwoFactorCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITotpService _totpService;
    private readonly IAESProvider _aesProvider;
    private readonly ILoginSecurityService _loginSecurityService;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisableTwoFactorCommandHandler"/> class.
    /// </summary>
    public DisableTwoFactorCommandHandler(DatabaseContext databaseContext, ITotpService totpService, IAESProvider aesProvider, ILoginSecurityService loginSecurityService, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _totpService = totpService;
        _aesProvider = aesProvider;
        _loginSecurityService = loginSecurityService;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.ContextUserId && u.IsDeleted == 0, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        if (!user.IsTotpEnabled)
        {
            throw new BusinessException("尚未开启两步验证.") { StatusCode = 400 };
        }

        if (await _loginSecurityService.IsForced2faAsync(cancellationToken))
        {
            throw new BusinessException("系统已强制开启两步验证，无法关闭.") { StatusCode = 400 };
        }

        var secret = _aesProvider.Decrypt(user.TotpSecret!);
        if (!_totpService.TryVerifyCode(secret, request.Code, out _))
        {
            throw new BusinessException("动态码错误，请输入认证器当前显示的 6 位数字.") { StatusCode = 400 };
        }

        user.IsTotpEnabled = false;
        user.TotpSecret = null;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
