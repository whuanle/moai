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

    public string? TemplateKey { get; set; }

    public string Mode { get; set; } = default!;

    public string? Database { get; set; }

    public long IsDeleted { get; set; }

    public long CreateUserId { get; set; }

    public DateTimeOffset CreateTime { get; set; }

    public long UpdateUserId { get; set; }

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
}
