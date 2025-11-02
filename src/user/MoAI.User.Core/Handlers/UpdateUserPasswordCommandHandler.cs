using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.User.Commands;
using System.Text.RegularExpressions;

namespace MoAI.User.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateUserPasswordCommand"/>
/// </summary>
public class UpdateUserPasswordCommandHandler : IRequestHandler<UpdateUserPasswordCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserPasswordCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="rsaProvider"></param>
    public UpdateUserPasswordCommandHandler(DatabaseContext databaseContext, IRsaProvider rsaProvider)
    {
        _databaseContext = databaseContext;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users.Where(x => x.Id == request.UserId).FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new BusinessException("用户不存在") { StatusCode = 400 };
        }

        if (user.IsDisable)
        {
            throw new BusinessException("用户已被禁用") { StatusCode = 400 };
        }

        // 使用 RSA 解密还原密码
        string restorePassword = default!;
        try
        {
            restorePassword = _rsaProvider.Decrypt(request.Password);
            Regex regex = new Regex(@"(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$");
            Match match = regex.Match(restorePassword);
            if (!match.Success)
            {
                throw new BusinessException("密码 8-30 长度，并包含数字+字母+特殊字符.") { StatusCode = 400 };
            }
        }
        catch
        {
            throw new BusinessException("密码验证失败") { StatusCode = 400 };
        }

        // 使用 PBKDF2 算法生成哈希值
        var (hashedPassword, saltBase64) = PBKDF2Helper.ToHash(restorePassword);
        user.Password = hashedPassword;
        user.PasswordSalt = saltBase64;

        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
