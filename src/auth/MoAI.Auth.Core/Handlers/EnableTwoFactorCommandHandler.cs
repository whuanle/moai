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
/// <inheritdoc cref="EnableTwoFactorCommand"/>.
/// </summary>
public class EnableTwoFactorCommandHandler : IRequestHandler<EnableTwoFactorCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITotpService _totpService;
    private readonly IAESProvider _aesProvider;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnableTwoFactorCommandHandler"/> class.
    /// </summary>
    public EnableTwoFactorCommandHandler(DatabaseContext databaseContext, ITotpService totpService, IAESProvider aesProvider, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _totpService = totpService;
        _aesProvider = aesProvider;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(EnableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.ContextUserId && u.IsDeleted == 0, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        if (user.IsTotpEnabled)
        {
            throw new BusinessException("已开启两步验证，无需重复绑定.") { StatusCode = 400 };
        }

        if (string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            throw new BusinessException("请先获取两步验证密钥并使用认证器扫码.") { StatusCode = 400 };
        }

        var secret = _aesProvider.Decrypt(user.TotpSecret);
        if (!_totpService.TryVerifyCode(secret, request.Code, out _))
        {
            throw new BusinessException("动态码错误，请输入认证器当前显示的 6 位数字.") { StatusCode = 400 };
        }

        user.IsTotpEnabled = true;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
