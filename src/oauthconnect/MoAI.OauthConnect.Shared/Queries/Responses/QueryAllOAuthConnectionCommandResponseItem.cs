using MoAI.Infra.Models;

namespace MoAI.OauthConnect.Queries.Responses;

/// <summary>
/// QueryAllOAuthConnectionCommandResponseItem.
/// </summary>
public class QueryAllOAuthConnectionCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 认证名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 图标地址.
    /// </summary>
    public string IconUrl { get; set; } = default!;

    /// <summary>
    /// 提供商标识.
    /// </summary>
    public string Provider { get; set; } = default!;

    /// <summary>
    /// 应用 key.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 发现端点.
    /// </summary>
    public string WellKnown { get; set; } = default!;

    /// <summary>
    /// 登录跳转地址.
    /// </summary>
    public string AuthorizeUrl { get; set; } = default!;
}
