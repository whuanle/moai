using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Auth.Commands;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Account.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="RegisterUserCommand"/>.
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, SimpleLong>
{
    private readonly DatabaseContext _dbContext;
    private readonly ILogger<RegisterUserCommandHandler> _logger;
    private readonly IRsaProvider _rsaProvider;
    private readonly IEmailCodeService _emailCodeService;
    private readonly IReadOnlyList<IUserRegistrationHook> _registrationHooks;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterUserCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="logger"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="emailCodeService">邮箱验证码服务（注册验证码开关开启时校验）.</param>
    /// <param name="registrationHooks">注册增强钩子（默认角色等），无实现时为空集合.</param>
    public RegisterUserCommandHandler(DatabaseContext dbContext, ILogger<RegisterUserCommandHandler> logger, IRsaProvider rsaProvider, IEmailCodeService emailCodeService, IEnumerable<IUserRegistrationHook> registrationHooks)
    {
        _dbContext = dbContext;
        _logger = logger;
        _rsaProvider = rsaProvider;
        _emailCodeService = emailCodeService;
        _registrationHooks = registrationHooks.ToList();
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
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
                .FirstOrDefaultAsync(cancellationToken);

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

        // 注册验证码：开关开启时必须携带邮箱验证码并校验通过（在插入用户的 try 之外校验，保留精确的业务状态码）
        if (await _emailCodeService.IsRegisterCodeRequiredAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(request.EmailCode))
            {
                throw new BusinessException("请输入邮箱验证码.") { StatusCode = 400 };
            }

            await _emailCodeService.VerifyAndConsumeAsync(EmailCodePurpose.Register, request.Email, request.EmailCode, cancellationToken);
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

            // 注册增强钩子（组织默认角色等）：在用户 id 生成后执行，只入列由主流程统一提交；
            // 尽力而为，失败不影响注册结果
            try
            {
                foreach (var hook in _registrationHooks)
                {
                    await hook.OnUserRegisteredAsync(userEntity.Id, cancellationToken);
                }

                if (_dbContext.ChangeTracker.HasChanges())
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception hookEx)
            {
                _logger.LogWarning(hookEx, "用户注册增强钩子执行失败，跳过附加动作.{UserId}", userEntity.Id);
            }

            return new SimpleLong
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
