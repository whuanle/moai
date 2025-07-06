// <copyright file="UpdateUserPasswordCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.User.Shared.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.RegularExpressions;

namespace MoAI.User.Handlers;

public class UpdateUserPasswordCommandHandler : IRequestHandler<UpdateUserPasswordCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRsaProvider _rsaProvider;

    public UpdateUserPasswordCommandHandler(DatabaseContext databaseContext, IRsaProvider rsaProvider)
    {
        _databaseContext = databaseContext;
        _rsaProvider = rsaProvider;
    }

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
