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
/// 飞书应用绑定，将飞书应用绑定到应用/知识库外部源等渠道；应用渠道独占，外部源等订阅型渠道可一对多.
/// </summary>
public partial class FeishuAppBindingEntity : IFullAudited
{
    /// <summary>
    /// 自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 飞书应用记录 id（feishu_app.id，逻辑关联，仓库约定不建物理外键）.
    /// </summary>
    public Guid FeishuAppId { get; set; }

    /// <summary>
    /// 渠道类型，见 FeishuChannelType（0=app 独占型，1=wikiSource 订阅型可一对多）.
    /// </summary>
    public int ChannelType { get; set; }

    /// <summary>
    /// 渠道记录 id 字符串，应用渠道为 app.id（uuid），知识库外部源渠道为 wiki_source.id（uuid）.
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
