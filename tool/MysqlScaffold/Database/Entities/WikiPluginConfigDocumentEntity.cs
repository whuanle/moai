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
/// 知识库文档关联任务，这里的任务都是成功的.
/// </summary>
[Table("wiki_plugin_config_document")]
public partial class WikiPluginConfigDocumentEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    [Column("wiki_id", TypeName = "int(11)")]
    public int WikiId { get; set; }

    /// <summary>
    /// 爬虫id.
    /// </summary>
    [Column("config_id", TypeName = "int(11)")]
    public int ConfigId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    [Column("wiki_document_id", TypeName = "int(11)")]
    public int WikiDocumentId { get; set; }

    /// <summary>
    /// 关联对象.
    /// </summary>
    [Column("relevance_key")]
    [StringLength(1000)]
    public string RelevanceKey { get; set; } = default!;

    /// <summary>
    /// 关联值.
    /// </summary>
    [Column("relevance_value")]
    [StringLength(1000)]
    public string RelevanceValue { get; set; } = default!;

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
    /// 更新人.
    /// </summary>
    [Column("update_user_id", TypeName = "int(11)")]
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    [Column("update_time", TypeName = "datetime")]
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    [Column("is_deleted", TypeName = "bigint(20)")]
    public long IsDeleted { get; set; }
}
