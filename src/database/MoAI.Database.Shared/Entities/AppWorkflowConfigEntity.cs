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
/// 草稿可反复编辑；发布后生成不可变的已发布定义快照（版本号递增）.
/// </summary>
public partial class AppWorkflowConfigEntity : IFullAudited
{
    /// <summary>
    /// 配置ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 所属应用ID，逻辑关联app.id（1:1，不建物理外键）.
    /// </summary>
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
    public DateTimeOffset? PublishTime { get; set; }

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
    /// 软删除，0=未删除（legacy bigint 约定）.
    /// </summary>
    public long IsDeleted { get; set; }
}
