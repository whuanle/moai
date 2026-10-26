using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.DingTalk.Queries.Responses;

/// <summary>
/// 钉钉应用连接项.
/// </summary>
public class DingTalkAppItem : AuditsInfo
{
    /// <summary>
    /// 钉钉应用记录 id.
    /// </summary>
    public Guid DingTalkAppId { get; set; }

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
    /// 钉钉应用 AppKey（新版开发者后台为 Client ID）.
    /// </summary>
    public string AppKey { get; set; } = default!;

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 长连接是否在线（禁用时恒为 false）.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// 绑定的渠道类型，未绑定为 null.
    /// </summary>
    public DingTalkChannelType? BindChannelType { get; set; }

    /// <summary>
    /// 绑定的渠道记录 id 字符串，未绑定为 null.
    /// </summary>
    public string? BindChannelId { get; set; }

    /// <summary>
    /// 绑定时间，未绑定为 null.
    /// </summary>
    public DateTimeOffset? BindTime { get; set; }
}
