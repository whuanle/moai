using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 通知中心渠道：统一承接飞书/钉钉/企业微信/Telegram/通用 webhook/SMTP/站内推送等通知出口配置.
/// </summary>
public partial class NotificationChannelEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 渠道名称（唯一）.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 渠道类型.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// 渠道配置 JSON（按渠道类型结构化，含 webhook 地址/密钥/收件人等）.
    /// </summary>
    public string Config { get; set; } = default!;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 渠道描述.
    /// </summary>
    public string Description { get; set; } = default!;

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
