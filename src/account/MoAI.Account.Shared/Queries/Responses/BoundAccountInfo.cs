namespace MoAI.Account.Queries.Responses;

/// <summary>
/// 已绑定的第三方账号信息.
/// </summary>
public class BoundAccountInfo
{
    /// <summary>
    /// 第三方认证方式 id，对应 OauthConnection 表的 id.
    /// </summary>
    public Guid OAuthId { get; set; }

    /// <summary>
    /// 认证方式名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 提供商标识.
    /// </summary>
    public string Provider { get; set; } = default!;

    /// <summary>
    /// 认证方式图标地址.
    /// </summary>
    public string IconUrl { get; set; } = default!;

    /// <summary>
    /// 绑定时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
