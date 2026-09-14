using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Feishu.Queries.Responses;

/// <summary>
/// 飞书应用连接项.
/// </summary>
public class FeishuAppItem : AuditsInfo
{
    /// <summary>
    /// 飞书应用记录 id.
    /// </summary>
    public Guid FeishuAppId { get; set; }

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
    /// 飞书开放平台 AppID.
    /// </summary>
    public string AppId { get; set; } = default!;

    /// <summary>
    /// 接入域名.
    /// </summary>
    public string Domain { get; set; } = default!;

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
    public FeishuChannelType? BindChannelType { get; set; }

    /// <summary>
    /// 绑定的渠道记录 id 字符串，未绑定为 null.
    /// </summary>
    public string? BindChannelId { get; set; }

    /// <summary>
    /// 绑定时间，未绑定为 null.
    /// </summary>
    public DateTimeOffset? BindTime { get; set; }
}
