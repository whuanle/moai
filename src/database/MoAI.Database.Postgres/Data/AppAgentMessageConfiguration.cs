using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database;

/// <summary>
/// Agent 应用会话消息（对话历史），追加写，按 seq 排序.
/// </summary>
internal partial class AppAgentMessageConfiguration : IEntityTypeConfiguration<AppAgentMessageEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAgentMessageEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_agent_message_pkey");

        entity.ToTable("app_agent_message", tb => tb.HasComment("Agent 应用会话消息（对话历史），追加写，按 seq 排序"));

        entity.HasIndex(e => new { e.SessionId, e.Seq }, "idx_app_agent_message_session_seq_uindex").IsUnique();

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("消息ID")
            .HasColumnName("id");
        entity.Property(e => e.CompletionsId)
            .HasMaxLength(50)
            .HasDefaultValueSql("''::character varying")
            .HasComment("模型一次补全的标识，同一次补全的多条消息共用一个；空串=无")
            .HasColumnName("completions_id");
        entity.Property(e => e.Content)
            .HasDefaultValueSql("''::text")
            .HasComment("消息正文，空串=无文本（如纯工具调用消息）")
            .HasColumnName("content");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除（legacy bigint 约定）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Reasoning)
            .HasDefaultValueSql("''::text")
            .HasComment("模型推理内容（思维链），空串=无")
            .HasColumnName("reasoning");
        entity.Property(e => e.Role)
            .HasMaxLength(20)
            .HasComment("角色：system|user|assistant|tool（对齐模型协议 role 取值）")
            .HasColumnName("role");
        entity.Property(e => e.Seq)
            .HasComment("会话内序号，从 1 递增，决定消息顺序（不依赖时间戳/UUID 排序）")
            .HasColumnName("seq");
        entity.Property(e => e.SessionId)
            .HasComment("所属会话ID，逻辑关联app_agent_session.id")
            .HasColumnName("session_id");
        entity.Property(e => e.ToolCallId)
            .HasMaxLength(50)
            .HasDefaultValueSql("''::character varying")
            .HasComment("role=tool 时对应的调用ID，回填 tool_calls[].id；空串=不适用")
            .HasColumnName("tool_call_id");
        entity.Property(e => e.ToolCalls)
            .HasDefaultValueSql("'[]'::text")
            .HasComment("assistant 请求的工具/插件调用列表，JSON 数组文本，空为 '[]'")
            .HasColumnName("tool_calls");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAgentMessageEntity> modelBuilder);
}
