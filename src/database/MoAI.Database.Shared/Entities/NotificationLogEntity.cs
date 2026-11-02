using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 通知发送记录：通知中心每次对单渠道投递一条，记录最终状态与耗时.
/// </summary>
public partial class NotificationLogEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 渠道 id.
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// 渠道名称快照（渠道删除后仍可读）.
    /// </summary>
    public string ChannelName { get; set; } = default!;

    /// <summary>
    /// 渠道类型.
    /// </summary>
    public int ChannelType { get; set; }

    /// <summary>
    /// 通知级别.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 通知标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 通知正文（超长截断存档）.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 关联用户 id 快照（逗号分隔，定向个人推送时记录收件人；空为广播/全员）.
    /// </summary>
    public string TargetUserIds { get; set; } = default!;

    /// <summary>
    /// 实际收件人快照（逗号分隔；SMTP 渠道记邮箱、短信渠道记手机号；固定地址渠道留空）.
    /// </summary>
    public string Recipients { get; set; } = default!;

    /// <summary>
    /// 发送状态.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 失败原因.
    /// </summary>
    public string Error { get; set; } = default!;

    /// <summary>
    /// 发送耗时（毫秒）.
    /// </summary>
    public long DurationMs { get; set; }

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
