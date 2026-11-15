using Microsoft.EntityFrameworkCore;
using MoAI.Account.Queries.Responses;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Storage.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Account.Services;

/// <summary>
/// 用户账号领域服务.
/// </summary>
public class UserAccountService : IUserAccountService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly IStorageService _storageService;
    private readonly IUserSecurityStateService _securityStateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAccountService"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="storageService"></param>
    /// <param name="securityStateService">用户安全态分布式缓存.</param>
    public UserAccountService(DatabaseContext databaseContext, IRedisDatabase redisDatabase, IStorageService storageService, IUserSecurityStateService securityStateService)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _storageService = storageService;
        _securityStateService = securityStateService;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> GetUserStateAsync(long userId, CancellationToken cancellationToken)
    {
        var key = $"userstate:{userId}";
        var securityState = await _securityStateService.GetAsync(userId, cancellationToken);
        var userState = await _redisDatabase.GetAsync<UserStateInfo>(key);
        if (userState != null && !string.IsNullOrWhiteSpace(userState.UserName))
        {
            // 安全字段不信任资料缓存：每次以共享安全态覆盖，避免并发回填把禁用/鉴权版本写成旧值
            userState.IsDisable = securityState.IsDisable;
            userState.IsDeleted = securityState.IsDeleted;
            userState.AuthVersion = securityState.AuthVersion;
            return userState;
        }

        var user = await _databaseContext.Users.Where(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);

        UserStateInfo result;
        if (user == null)
        {
            result = new UserStateInfo
            {
                IsDeleted = true,
                IsDisable = true,
                UserId = userId,
                AuthVersion = securityState.AuthVersion
            };
        }
        else
        {
            var avatar = string.IsNullOrWhiteSpace(user.AvatarPath)
                ? string.Empty
                : _storageService.GetPublicFileUrl(user.AvatarPath).ToString();

            var isRoot = await _databaseContext.Settings.AnyAsync(s => s.Key == "root" && s.Value == user.Id.ToString(), cancellationToken);

            result = new UserStateInfo
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                NickName = user.NickName,
                Phone = user.Phone,
                IsDisable = securityState.IsDisable,
                IsAdmin = user.IsAdmin || isRoot,
                IsRoot = isRoot,
                IsDeleted = securityState.IsDeleted,
                Avatar = avatar,
                TwoFactorEnabled = user.IsTotpEnabled,
                AuthVersion = securityState.AuthVersion
            };
        }

        await _redisDatabase.Database.StringSetAsync(key, result.ToRedisValue());
        await _redisDatabase.Database.KeyExpireAsync(key, TimeSpan.FromHours(1));

        return result;
    }

    /// <inheritdoc/>
    public async Task RemoveUserStateAsync(long userId, CancellationToken cancellationToken)
    {
        var key = $"userstate:{userId}";
        await _redisDatabase.Database.KeyDeleteAsync(key);
    }
}
