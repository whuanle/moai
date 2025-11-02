namespace MoAI.Login.Queries.Responses;

/// <summary>
/// QueryAllOAuthPrividerCommandResponseItem.
/// </summary>
public class QueryAllOAuthPrividerCommandResponseItem
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthId { get; init; } = default!;

    /// <summary>
    /// 提供商名称
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 提供商图标地址
    /// </summary>
    public string IconUrl { get; set; } = default!;

    /// <summary>
    /// 提供商标识
    /// </summary>
    public string Provider { get; set; } = default!;

    /// <summary>
    /// 授权地址.
    /// </summary>
    public string RedirectUrl { get; set; } = default!;
}
