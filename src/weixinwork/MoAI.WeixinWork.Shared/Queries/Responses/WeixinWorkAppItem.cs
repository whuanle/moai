using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.WeixinWork.Queries.Responses;

/// <summary>
/// 企业微信应用连接项.
/// </summary>
public class WeixinWorkAppItem : AuditsInfo
{
    /// <summary>
    /// 企业微信应用记录 id.
    /// </summary>
    public Guid WeixinWorkAppId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 连接名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 企业微信企业 ID.
    /// </summary>
    public string CorpId { get; set; } = default!;

    /// <summary>
    /// 企业微信自建应用 AgentId.
    /// </summary>
    public int AgentId { get; set; }

    /// <summary>
    /// API 地址覆盖，空表示使用官方地址.
    /// </summary>
    public string ApiEndpoint { get; set; } = default!;

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否可接收回调推送（回调模式下连接启用即可接收，禁用时恒为 false）.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// 绑定的渠道类型，未绑定为 null.
    /// </summary>
    public WeixinWorkChannelType? BindChannelType { get; set; }

    /// <summary>
    /// 绑定的渠道记录 id 字符串，未绑定为 null.
    /// </summary>
    public string? BindChannelId { get; set; }

    /// <summary>
    /// 绑定时间，未绑定为 null.
    /// </summary>
    public DateTimeOffset? BindTime { get; set; }
}
