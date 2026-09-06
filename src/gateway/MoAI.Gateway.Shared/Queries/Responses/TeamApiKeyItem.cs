namespace MoAI.Gateway.Queries.Responses;

/// <summary>
/// 网关 API Key 项.
/// </summary>
public class TeamApiKeyItem
{
    /// <summary>
    /// 密钥 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 密钥名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 密钥前缀，如 moai-Ab12CdEf.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否已过期.
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// 过期时间，null=永不过期.
    /// </summary>
    public DateTimeOffset? ExpireTime { get; set; }

    /// <summary>
    /// 最近一次调用时间，null=从未使用.
    /// </summary>
    public DateTimeOffset? LastUsedTime { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
