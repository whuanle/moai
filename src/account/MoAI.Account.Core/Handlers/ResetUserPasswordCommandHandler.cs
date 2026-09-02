using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using MoAI.Account.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Account.Services;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="ResetUserPasswordCommand"/>
/// </summary>
public class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetUserPasswordCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    /// <param name="rsaProvider"></param>
    public ResetUserPasswordCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, IRsaProvider rsaProvider)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
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
            throw new BusinessException("不能重置超级管理员账号的密码.") { StatusCode = 400 };
        }

        if (user.IsAdmin && request.ContextUserId.ToString() != rootValue)
        {
            throw new BusinessException("管理员不能重置其他管理员的密码.") { StatusCode = 403 };
        }

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

        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
