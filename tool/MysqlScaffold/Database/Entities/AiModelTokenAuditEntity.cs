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
/// 统计不同模型的token使用量，该表不是实时刷新的.
/// </summary>
[Table("ai_model_token_audit")]
public partial class AiModelTokenAuditEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 模型id.
    /// </summary>
    [Column("model_id", TypeName = "int(11)")]
    public int ModelId { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    [Column("useri_id", TypeName = "int(11)")]
    public int UseriId { get; set; }

    /// <summary>
    /// 完成数量.
    /// </summary>
    [Column("completion_tokens", TypeName = "int(11)")]
    public int CompletionTokens { get; set; }

    /// <summary>
    /// 输入数量.
    /// </summary>
    [Column("prompt_tokens", TypeName = "int(11)")]
    public int PromptTokens { get; set; }

    /// <summary>
    /// 总数量.
    /// </summary>
    [Column("total_tokens", TypeName = "int(11)")]
    public int TotalTokens { get; set; }

    /// <summary>
    /// 调用次数.
    /// </summary>
    [Column("count", TypeName = "int(11)")]
    public int Count { get; set; }

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
