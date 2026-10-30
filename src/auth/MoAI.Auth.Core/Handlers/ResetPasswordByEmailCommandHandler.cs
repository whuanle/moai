using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using MoAI.Account.Services;
using MoAI.Auth.Commands;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="ResetPasswordByEmailCommand"/>.
/// </summary>
public class ResetPasswordByEmailCommandHandler : IRequestHandler<ResetPasswordByEmailCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IEmailCodeService _emailCodeService;
    private readonly IUserAccountService _userAccountService;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPasswordByEmailCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext">数据库上下文.</param>
    /// <param name="emailCodeService">邮箱验证码服务.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="rsaProvider">RSA 加解密服务.</param>
    public ResetPasswordByEmailCommandHandler(
        DatabaseContext dbContext,
        IEmailCodeService emailCodeService,
        IUserAccountService userAccountService,
        IRsaProvider rsaProvider)
    {
        _dbContext = dbContext;
        _emailCodeService = emailCodeService;
        _userAccountService = userAccountService;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ResetPasswordByEmailCommand request, CancellationToken cancellationToken)
    {
        if (!await _emailCodeService.IsPasswordResetEnabledAsync(cancellationToken))
        {
            throw new BusinessException("SMTP 邮件服务未启用，无法通过邮箱找回密码，请联系管理员重置.") { StatusCode = 400 };
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsDeleted == 0, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("该邮箱未注册.") { StatusCode = 404 };
        }

        await _emailCodeService.VerifyAndConsumeAsync(EmailCodePurpose.ResetPassword, email, request.Code, cancellationToken);

        // 使用 RSA 解密还原密码，校验规则与注册保持一致
        string restorePassword;
        try
        {
            restorePassword = _rsaProvider.Decrypt(request.NewPassword);
            Regex regex = new(@"(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$");
            if (!regex.Match(restorePassword).Success)
            {
                throw new BusinessException("密码 8-20 长度，并包含数字+字母.") { StatusCode = 400 };
            }
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new BusinessException("密码解密失败，请刷新页面后重试.") { StatusCode = 400 };
        }

        var (hashedPassword, saltBase64) = PBKDF2Helper.ToHash(restorePassword);
        user.Password = hashedPassword;
        user.PasswordSalt = saltBase64;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
