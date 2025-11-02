using MoAI.Infra.Models;

namespace MoAI.Admin.OAuth.Queries.Responses;

/// <summary>
/// 描述.
/// </summary>
public class OAuthPrividerDetailItem : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; init; }

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
    /// key.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 发现端口
    /// </summary>
    public string WellKnown { get; set; } = default!;

    /// <summary>
    /// 回调地址.
    /// </summary>
    public string AuthorizeUrl { get; set; } = default!;
}
