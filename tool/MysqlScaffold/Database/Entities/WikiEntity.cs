using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
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
[Table("wiki")]
public partial class WikiEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    [Column("name")]
    [StringLength(20)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// 知识库描述.
    /// </summary>
    [Column("description")]
    [StringLength(255)]
    public string Description { get; set; } = default!;

    /// <summary>
    /// 是否公开，公开后所有人都可以使用，但是不能进去操作.
    /// </summary>
    [Column("is_public")]
    public bool IsPublic { get; set; }

    /// <summary>
    /// 是否已被锁定配置.
    /// </summary>
    [Column("is_lock")]
    public bool IsLock { get; set; }

    /// <summary>
    /// 向量化模型的id.
    /// </summary>
    [Column("embedding_model_id", TypeName = "int(11)")]
    public int EmbeddingModelId { get; set; }

    /// <summary>
    /// 知识库向量维度.
    /// </summary>
    [Column("embedding_dimensions", TypeName = "int(11)")]
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// 团队头像.
    /// </summary>
    [Column("avatar")]
    [StringLength(255)]
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 团队id，不填则是个人知识库.
    /// </summary>
    [Column("team_id", TypeName = "int(11)")]
    public int TeamId { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    [Column("create_user_id", TypeName = "int(11)")]
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    [Column("create_time", TypeName = "datetime")]
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    [Column("update_user_id", TypeName = "int(11)")]
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    [Column("update_time", TypeName = "datetime")]
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    [Column("is_deleted", TypeName = "bigint(20)")]
    public long IsDeleted { get; set; }
}
