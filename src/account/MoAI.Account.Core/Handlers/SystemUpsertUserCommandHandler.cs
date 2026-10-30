using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Account.Commands;
using MoAI.Account.Queries.Responses;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="SystemUpsertUserCommand"/>
/// </summary>
public class SystemUpsertUserCommandHandler : IRequestHandler<SystemUpsertUserCommand, SystemUpsertUserCommandResponse>
{
    private static readonly Regex PasswordRegex = new(@"(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$", RegexOptions.Compiled);

    private const string PasswordPolicyMessage = "密码 8-20 长度，并包含数字+字母.";

    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly IEnumerable<IUserRegistrationHook> _registrationHooks;
    private readonly ILogger<SystemUpsertUserCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemUpsertUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="registrationHooks">注册增强钩子（组织默认角色等）.</param>
    /// <param name="logger">日志.</param>
    public SystemUpsertUserCommandHandler(
        DatabaseContext databaseContext,
        IUserAccountService userAccountService,
        IEnumerable<IUserRegistrationHook> registrationHooks,
        ILogger<SystemUpsertUserCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _registrationHooks = registrationHooks;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SystemUpsertUserCommandResponse> Handle(SystemUpsertUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken);

        if (user != null)
        {
            return await UpdateAsync(user, request, cancellationToken);
        }

        return await CreateAsync(request, cancellationToken);
    }

    private async Task<SystemUpsertUserCommandResponse> UpdateAsync(Database.Entities.UserEntity user, SystemUpsertUserCommand request, CancellationToken cancellationToken)
    {
        if (request.IsDisable == true)
        {
            var rootValue = await _databaseContext.Settings
                .Where(s => s.Key == "root")
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            if (user.Id.ToString() == rootValue)
            {
                throw new BusinessException("不能禁用超级管理员账号.") { StatusCode = 400 };
            }
        }

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            await EnsureEmailAvailableAsync(request.Email, user.Id, cancellationToken);
            user.Email = request.Email;
        }

        if (!string.IsNullOrEmpty(request.Phone) && request.Phone != user.Phone)
        {
            await EnsurePhoneAvailableAsync(request.Phone, user.Id, cancellationToken);
            user.Phone = request.Phone;
        }

        if (!string.IsNullOrEmpty(request.NickName))
        {
            user.NickName = request.NickName;
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            user.Password = HashPassword(request.Password, out var salt);
            user.PasswordSalt = salt;
        }

        if (request.IsDisable.HasValue)
        {
            user.IsDisable = request.IsDisable.Value;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        return new SystemUpsertUserCommandResponse { UserId = user.Id, Created = false };
    }

    private async Task<SystemUpsertUserCommandResponse> CreateAsync(SystemUpsertUserCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Email))
        {
            await EnsureEmailAvailableAsync(request.Email, userId: 0, cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.Phone))
        {
            await EnsurePhoneAvailableAsync(request.Phone, userId: 0, cancellationToken);
        }

        // 未提供密码时生成随机初始密码，仅在本次响应中返回一次
        var initialPassword = request.Password;
        var generated = false;
        if (string.IsNullOrEmpty(initialPassword))
        {
            initialPassword = GeneratePassword();
            generated = true;
        }
        else if (!PasswordRegex.IsMatch(initialPassword))
        {
            throw new BusinessException(PasswordPolicyMessage) { StatusCode = 400 };
        }

        // email/phone 列非空且唯一（(值,IsDeleted) 唯一索引）：未提供时生成唯一占位值，
        // 与 OAuth 注册同口径（email=用户名@moai.com，phone=9 前缀稳定哈希占位号避开真实号段），后续推送真实值时覆盖
        var user = new Database.Entities.UserEntity
        {
            UserName = request.UserName,
            NickName = string.IsNullOrEmpty(request.NickName) ? request.UserName : request.NickName,
            Email = string.IsNullOrEmpty(request.Email) ? request.UserName + "@moai.com" : request.Email,
            Phone = string.IsNullOrEmpty(request.Phone) ? GeneratePlaceholderPhone(request.UserName) : request.Phone,
            Password = HashPassword(initialPassword, out var salt),
            PasswordSalt = salt,
            AvatarPath = string.Empty,
            IsDisable = request.IsDisable ?? false,
            IsAdmin = false,
        };

        await _databaseContext.Users.AddAsync(user, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 注册增强钩子（组织默认角色等）：与自注册同口径，尽力而为，失败不影响建户
        try
        {
            foreach (var hook in _registrationHooks)
            {
                await hook.OnUserRegisteredAsync(user.Id, cancellationToken);
            }

            if (_databaseContext.ChangeTracker.HasChanges())
            {
                await _databaseContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception hookEx)
        {
            _logger.LogWarning(hookEx, "系统建户增强钩子执行失败，跳过附加动作.{UserId}", user.Id);
        }

        return new SystemUpsertUserCommandResponse
        {
            UserId = user.Id,
            Created = true,
            InitialPassword = generated ? initialPassword : null,
        };
    }

    private async Task EnsureEmailAvailableAsync(string email, long userId, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.Users
            .AnyAsync(x => x.Email == email && x.Id != userId, cancellationToken);
        if (exist)
        {
            throw new BusinessException("邮箱已被其他用户绑定.") { StatusCode = 409 };
        }
    }

    private async Task EnsurePhoneAvailableAsync(string phone, long userId, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.Users
            .AnyAsync(x => x.Phone == phone && x.Id != userId, cancellationToken);
        if (exist)
        {
            throw new BusinessException("手机号已被其他用户绑定.") { StatusCode = 409 };
        }
    }

    private static string HashPassword(string password, out string salt)
    {
        var (hashedPassword, saltBase64) = PBKDF2Helper.ToHash(password);
        salt = saltBase64;
        return hashedPassword;
    }

    private static string GeneratePlaceholderPhone(string userName)
    {
        // phone 列非空且唯一：按用户名稳定哈希生成占位号（9 前缀避开真实号段），与 OAuth 注册同口径
        var hashHex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(userName)));
        var digits = long.Parse(hashHex[..13], System.Globalization.NumberStyles.HexNumber).ToString();
        return "9" + digits.PadRight(10, '0')[..10];
    }

    private static string GeneratePassword()
    {
        // 保证满足强度策略：大写 + 小写 + 数字 + 字母数字填充，共 12 位
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        var sb = new StringBuilder(12);
        sb.Append(upper[RandomNumberGenerator.GetInt32(upper.Length)]);
        sb.Append(lower[RandomNumberGenerator.GetInt32(lower.Length)]);
        sb.Append(digits[RandomNumberGenerator.GetInt32(digits.Length)]);
        for (var i = sb.Length; i < 12; i++)
        {
            sb.Append(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        return sb.ToString();
    }
}
