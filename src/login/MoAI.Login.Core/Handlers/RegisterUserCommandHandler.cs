// <copyright file="RegisterUserCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Login.Commands;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="RegisterUserCommand"/>.
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, SimpleInt>
{
    private readonly DatabaseContext _dbContext;
    private readonly ILogger<RegisterUserCommandHandler> _logger;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterUserCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="logger"></param>
    /// <param name="rsaProvider"></param>
    public RegisterUserCommandHandler(DatabaseContext dbContext, ILogger<RegisterUserCommandHandler> logger, IRsaProvider rsaProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var disableRegister = await _dbContext.Settings
            .Where(s => s.Key == SystemSettingKeys.DisableRegister)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (disableRegister == "true" || disableRegister == "1")
        {
            // 禁用注册功能
            _logger.LogWarning("注册功能已被禁用，请联系管理员。");
            throw new BusinessException("注册功能已被禁用，请联系管理员。") { StatusCode = 403 };
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
                throw new BusinessException("密码 8-20 长度，并包含数字+字母.") { StatusCode = 400 };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            throw new BusinessException("密码 8-20 长度，并包含数字+字母.") { StatusCode = 400 };
        }

        var existingUser = await _dbContext.Users
                .Where(u => u.UserName == request.UserName || u.Email == request.Email || u.Phone == request.Phone)
                .Select(u => new { u.UserName, u.Email, u.Phone })
                .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            if (existingUser.UserName == request.UserName)
            {
                throw new BusinessException("用户名 {0} 已被注册", request.UserName) { StatusCode = 409 };
            }

            if (existingUser.Email == request.Email)
            {
                throw new BusinessException("邮箱 {0} 已被注册", request.Email) { StatusCode = 409 };
            }

            if (existingUser.Phone == request.Phone)
            {
                throw new BusinessException("手机号 {0} 已被注册", request.Phone) { StatusCode = 409 };
            }
        }

        try
        {
            // 使用 PBKDF2 算法生成哈希值
            var (hashedPassword, saltBase64) = PBKDF2Helper.ToHash(restorePassword);
            var userEntity = new UserEntity
            {
                Email = request.Email,
                NickName = request.NickName,
                UserName = request.UserName,
                Password = hashedPassword,
                PasswordSalt = saltBase64,
                AvatarPath = string.Empty,
                Phone = request.Phone,
            };

            await _dbContext.Users.AddAsync(userEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new SimpleInt
            {
                Value = userEntity.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account registration failed.{@Command}", request);
            throw new BusinessException("账号注册失败");
        }
    }
}