using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 钉钉应用，一个钉钉开放平台企业内部应用对应一条 Stream 长连接，机器人消息统一接收后按绑定转发.
/// </summary>
public partial class DingTalkAppEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 连接名称，团队内唯一.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 钉钉应用 AppKey（新版开发者后台为 Client ID），全局唯一.
    /// </summary>
    public string AppKey { get; set; } = default!;

    /// <summary>
    /// 钉钉应用 AppSecret（新版开发者后台为 Client Secret）.
    /// </summary>
    public string AppSecret { get; set; } = default!;

    /// <summary>
    /// 机器人的 dingtalk id（回调报文 chatbotUserId，加密串），首次收到机器人消息时回填；用于生成拉起机器人单聊的 applink.
    /// </summary>
    public string ChatbotUserId { get; set; } = default!;

    /// <summary>
    /// 禁用，禁用后断开长连接且不再接收事件.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
