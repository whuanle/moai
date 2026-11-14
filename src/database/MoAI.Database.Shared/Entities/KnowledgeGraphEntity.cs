using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

public partial class KnowledgeGraphEntity : IFullAudited
{
    public long Id { get; set; }

    public int TeamId { get; set; }

    public string Name { get; set; } = default!;

    public string Description { get; set; } = default!;

    public string Mode { get; set; } = default!;

    public string? Database { get; set; }

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

    /// <summary>
    /// 头像地址.
    /// </summary>
    public string AvatarPath { get; set; } = default!;

    /// <summary>
    /// 向量化模型的id；为空表示未配置、图谱不参与向量检索.
    /// </summary>
    public Guid? EmbeddingModelId { get; set; }

    /// <summary>
    /// 知识图谱向量维度（1-2000，建 hnsw 索引的硬上限）.
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// 默认抽取模型的id（AI 导入缺省对话模型，D30 三级回退的图谱级默认）；为空表示未配置、导入时必须显式指定.
    /// </summary>
    public Guid? ExtractModelId { get; set; }

    /// <summary>
    /// 画布样式规则 JSON（B8：按实体类型配置节点第二行 caption 显示什么；空串=未配置、画布按默认显示类型名）.
    /// </summary>
    public string CanvasStyleJson { get; set; } = string.Empty;

    /// <summary>
    /// 是否开启 MCP 接入（/api/teamres/knowledge-graph/{id}/mcp 与该图外部资源 key，仅托管图）；开启时自动生成/复用该图唯一的团队资源 key.
    /// </summary>
    public bool IsMcpEnabled { get; set; }
}
