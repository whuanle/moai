using System.Security.Cryptography;
using System.Text.Json;
using Maomi;
using MoAI.Subsystem.Services;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// 委托 SSO 一次性委托码存储：Redis 键 + TTL 60s，兑换即删除（GetDelete 原子消费，不可重放）.
/// </summary>
[InjectOnScoped]
public class SubsystemSsoCodeStore : ISubsystemSsoCodeStore
{
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemSsoCodeStore"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public SubsystemSsoCodeStore(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <summary>
    /// 签发委托码（64 位随机 hex），值绑定子系统与用户.
    /// </summary>
    /// <param name="subsystemId">子系统 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <returns>返回委托码原文.</returns>
    public async Task<string> CreateAsync(Guid subsystemId, long userId)    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var payload = JsonSerializer.Serialize(new SsoCodePayload { SubsystemId = subsystemId, UserId = userId });
        await _redisDatabase.Database.StringSetAsync(
            SubsystemAuthDefaults.SsoCodeKeyPrefix + code,
            payload,
            SubsystemAuthDefaults.SsoCodeLifetime);
        return code;
    }

    /// <summary>
    /// 原子消费委托码：不存在/已消费/已过期返回 null.
    /// </summary>
    /// <param name="code">委托码原文.</param>
    /// <returns>返回绑定的子系统与用户，无效为 null.</returns>
    public async Task<SsoCodePayload?> ConsumeAsync(string code)
    {
        var value = await _redisDatabase.Database.StringGetDeleteAsync(SubsystemAuthDefaults.SsoCodeKeyPrefix + code);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SsoCodePayload>(value.ToString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 委托码绑定主体.
    /// </summary>
    public sealed record SsoCodePayload
    {
        /// <summary>
        /// 子系统 id.
        /// </summary>
        public Guid SubsystemId { get; init; }

        /// <summary>
        /// 用户 id.
        /// </summary>
        public long UserId { get; init; }
    }
}
