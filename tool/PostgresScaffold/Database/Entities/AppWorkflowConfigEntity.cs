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
/// 流程应用编排配置，与 app 一一对应（app_type=1）.
/// </summary>
public partial class AppWorkflowConfigEntity : IFullAudited
{
    public Guid Id { get; set; }

    public int TeamId { get; set; }

    public Guid AppId { get; set; }

    /// <summary>
    /// 草稿流程定义 JSON（引擎 WorkflowDefinition 契约：nodes + connections + ui）.
    /// </summary>
    public string DraftDefinition { get; set; } = default!;

    /// <summary>
    /// 草稿编辑器原始 JSON（FlowGram 画布 toJSON 产物，用于无损还原画布），空为 &apos;{}&apos;.
    /// </summary>
    public string DraftEditorData { get; set; } = default!;

    /// <summary>
    /// 已发布定义快照 JSON，发布后不可变；从未发布为 null.
    /// </summary>
    public string? PublishedDefinition { get; set; }

    /// <summary>
    /// 当前已发布版本号，发布一次递增 1，0=从未发布.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 状态，0=草稿有未发布变更（或从未发布） 1=当前草稿已发布（草稿与已发布版本一致）.
    /// </summary>
    public short Status { get; set; }

    /// <summary>
    /// 最近发布时间，从未发布为 null.
    /// </summary>
    public DateTime? PublishTime { get; set; }

    public long CreateUserId { get; set; }

    public DateTimeOffset CreateTime { get; set; }

    public long UpdateUserId { get; set; }

    public DateTimeOffset UpdateTime { get; set; }

    public long IsDeleted { get; set; }
}
