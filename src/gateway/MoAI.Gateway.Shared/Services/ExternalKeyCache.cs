using Maomi;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Gateway.Services;

/// <summary>
/// 应用接入 key 的 Redis 快照缓存：缓存可用接入实例快照（含功能范围），
/// 供管理端删除/改范围后的失效钩子与高频校验路径使用；
/// 变更时按 key 摘要删除缓存，未命中写路径由 TTL 兜底.
/// </summary>
[InjectOnScoped]
public class ExternalKeyCache
{
    private const string AccessKeyPrefix = "externalkey:ac:";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalKeyCache"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库实例.</param>
    public ExternalKeyCache(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <summary>
    /// 应用接入 key 缓存快照.
    /// </summary>
    public class AccessKeySnapshot
    {
        /// <summary>
        /// 接入 id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 归属团队 id.
        /// </summary>
        public int TeamId { get; set; }

        /// <summary>
        /// 功能范围位标记.
        /// </summary>
        public int Scopes { get; set; }
    }

    /// <summary>
    /// 读取应用接入 key 快照，未命中返回 null.
    /// </summary>
    /// <param name="keySha256Hex">key 明文的 sha256（小写 hex）.</param>
    /// <returns>返回缓存快照或 null.</returns>
    public Task<AccessKeySnapshot?> GetAccessKeyAsync(string keySha256Hex)
    {
        return _redisDatabase.GetAsync<AccessKeySnapshot>(AccessKeyPrefix + keySha256Hex);
    }

    /// <summary>
    /// 写入应用接入 key 快照.
    /// </summary>
    /// <param name="keySha256Hex">key 明文的 sha256（小写 hex）.</param>
    /// <param name="snapshot">快照.</param>
    public async Task SetAccessKeyAsync(string keySha256Hex, AccessKeySnapshot snapshot)
    {
        await _redisDatabase.Database.StringSetAsync(AccessKeyPrefix + keySha256Hex, snapshot.ToRedisValue());
        await _redisDatabase.Database.KeyExpireAsync(AccessKeyPrefix + keySha256Hex, CacheTtl);
    }

    /// <summary>
    /// 删除应用接入 key 缓存（删除/改范围后调用）.
    /// </summary>
    /// <param name="keySha256Hex">key 明文的 sha256（小写 hex）.</param>
    public Task RemoveAccessKeyAsync(string keySha256Hex)
    {
        return _redisDatabase.Database.KeyDeleteAsync(AccessKeyPrefix + keySha256Hex);
    }
}
