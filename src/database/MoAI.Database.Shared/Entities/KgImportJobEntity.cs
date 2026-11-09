using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

public partial class KgImportJobEntity : IFullAudited
{
    public long Id { get; set; }

    /// <summary>
    /// 所属图谱 id（仅托管图）.
    /// </summary>
    public long KnowledgeGraphId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 任务类型：file=AI 文档导入；json=JSON 结构化导入（预留，本期仅 file）.
    /// </summary>
    public string Type { get; set; } = "file";

    /// <summary>
    /// 导入文件名.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 导入文件的对象存储 key.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 抽取使用的对话模型 id.
    /// </summary>
    public Guid? AiModelId { get; set; }

    /// <summary>
    /// 任务状态：pending/extracting/parsing/merging/done/failed/cancelled.
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 总行数（解析出的节点+边草稿数，解析后回填）.
    /// </summary>
    public int TotalRows { get; set; }

    /// <summary>
    /// 已完成行数（落库进度）.
    /// </summary>
    public int DoneRows { get; set; }

    /// <summary>
    /// 失败原因（JSON：message/stage），成功为 null.
    /// </summary>
    public string? ErrorJson { get; set; }

    /// <summary>
    /// 结果统计（JSON：nodesCreated/nodesMerged/edgesCreated/skippedNodes/skippedEdges/contentLength/truncated/message），未完成为 null.
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>
    /// 软删除，0=未删除.
    /// </summary>
    public long IsDeleted { get; set; }

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
}
