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
/// 飞书应用，一个飞书开放平台应用对应一条长连接，事件统一接收后按绑定转发.
/// </summary>
public partial class FeishuAppEntity : IFullAudited
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
    /// 飞书开放平台 AppID，形如 cli_xxx，全局唯一.
    /// </summary>
    public string AppId { get; set; } = default!;

    /// <summary>
    /// 飞书开放平台 AppSecret.
    /// </summary>
    public string AppSecret { get; set; } = default!;

    /// <summary>
    /// 接入域名，飞书为 https://open.feishu.cn，Lark 为 https://open.larksuite.com.
    /// </summary>
    public string Domain { get; set; } = default!;

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
