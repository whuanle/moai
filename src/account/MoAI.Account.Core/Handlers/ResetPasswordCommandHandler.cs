using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="ResetPasswordCommand"/>
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPasswordCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    /// <param name="rsaProvider"></param>
    public ResetPasswordCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, IRsaProvider rsaProvider)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users.FirstOrDefaultAsync(x => x.Id == request.ContextUserId, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在") { StatusCode = 404 };
        }

        // 校验原密码
        try
        {
            var oldPassword = _rsaProvider.Decrypt(request.OldPassword);
            if (!PBKDF2Helper.VerifyHash(oldPassword, user.Password, user.PasswordSalt))
            {
                throw new BusinessException("原密码错误") { StatusCode = 400 };
            }
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            throw new BusinessException("原密码错误") { StatusCode = 400 };
        }

        // 校验并解密新密码
        string newPassword = default!;
        try
        {
            newPassword = _rsaProvider.Decrypt(request.NewPassword);
            var regex = new Regex(@"(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$");
            if (!regex.IsMatch(newPassword))
            {
                throw new BusinessException("密码 8-20 长度，并包含数字+字母.") { StatusCode = 400 };
            }
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            throw new BusinessException("密码 8-20 长度，并包含数字+字母.") { StatusCode = 400 };
        }

        // 使用 PBKDF2 算法生成新哈希
        var (hashedPassword, saltBase64) = PBKDF2Helper.ToHash(newPassword);
        user.Password = hashedPassword;
        user.PasswordSalt = saltBase64;

        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _userAccountService.RemoveUserStateAsync(request.ContextUserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
