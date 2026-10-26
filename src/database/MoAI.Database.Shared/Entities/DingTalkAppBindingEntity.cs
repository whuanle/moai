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
/// 钉钉应用绑定，将钉钉应用绑定到团队应用等渠道；应用渠道独占.
/// </summary>
public partial class DingTalkAppBindingEntity : IFullAudited
{
    /// <summary>
    /// 自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 钉钉应用记录 id（dingtalk_app.id，逻辑关联，仓库约定不建物理外键）.
    /// </summary>
    public Guid DingTalkAppId { get; set; }

    /// <summary>
    /// 渠道类型，见 DingTalkChannelType（当前仅 0=app 独占型）.
    /// </summary>
    public int ChannelType { get; set; }

    /// <summary>
    /// 渠道记录 id 字符串，应用渠道为 app.id（uuid）.
    /// </summary>
    public string ChannelId { get; set; } = default!;

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
