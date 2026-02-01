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
/// 知识库.
/// </summary>
public partial class WikiEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 知识库描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 是否公开，公开后所有人都可以使用，但是不能进去操作.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 计数器.
    /// </summary>
    public int Counter { get; set; }

    /// <summary>
    /// 是否已被锁定配置.
    /// </summary>
    public bool IsLock { get; set; }

    /// <summary>
    /// 向量化模型的id.
    /// </summary>
    public int EmbeddingModelId { get; set; }

    /// <summary>
    /// 知识库向量维度.
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// 团队头像.
    /// </summary>
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 团队id，不填则是个人知识库.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
