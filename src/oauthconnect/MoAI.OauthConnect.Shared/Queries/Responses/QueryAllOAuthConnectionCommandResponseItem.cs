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
    /// 企业微信应用 agentid，仅 weixinwork 提供商使用.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// 登录跳转地址.
    /// </summary>
    public string AuthorizeUrl { get; set; } = default!;

    /// <summary>
    /// 是否显示到登录页与个人中心绑定页；false=隐藏连接器（纯身份映射用）.
    /// </summary>
    public bool IsVisible { get; set; } = true;
}
