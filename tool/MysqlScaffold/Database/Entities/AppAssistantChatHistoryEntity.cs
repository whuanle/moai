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
/// 对话历史，不保存实际历史记录.
/// </summary>
[Table("app_assistant_chat_history")]
[Index("ChatId", Name = "chat_history_pk_2")]
public partial class AppAssistantChatHistoryEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "bigint(20)")]
    public long Id { get; set; }

    /// <summary>
    /// 对话id.
    /// </summary>
    [Column("chat_id")]
    public Guid ChatId { get; set; }

    /// <summary>
    /// 对话id.
    /// </summary>
    [Column("completions_id")]
    [StringLength(50)]
    public string CompletionsId { get; set; } = default!;

    /// <summary>
    /// 角色.
    /// </summary>
    [Column("role")]
    [StringLength(20)]
    public string Role { get; set; } = default!;

    /// <summary>
    /// 内容.
    /// </summary>
    [Column("content", TypeName = "text")]
    public string Content { get; set; } = default!;

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
