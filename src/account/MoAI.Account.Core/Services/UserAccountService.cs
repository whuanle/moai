using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Account.Queries.Responses;
using MoAI.Account.Services;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAccountService"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="storageService"></param>
    public UserAccountService(DatabaseContext databaseContext, IRedisDatabase redisDatabase, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> GetUserStateAsync(long userId, CancellationToken cancellationToken)
    {
        var key = $"userstate:{userId}";
        var userState = await _redisDatabase.GetAsync<UserStateInfo>(key);
        if (userState != null && !string.IsNullOrWhiteSpace(userState.UserName))
        {
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
                UserId = userId
            };
        }
        else
        {
            var avatar = string.IsNullOrWhiteSpace(user.AvatarPath)
                ? string.Empty
                : _storageService.GetPublicFileUrl(user.AvatarPath).ToString();

            result = new UserStateInfo
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                NickName = user.NickName,
                Phone = user.Phone,
                IsDisable = user.IsDisable,
                IsAdmin = user.IsAdmin,
                IsDeleted = user.IsDeleted > 0,
                Avatar = avatar
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
