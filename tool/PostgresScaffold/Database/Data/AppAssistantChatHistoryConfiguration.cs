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
/// 对话历史，不保存实际历史记录.
/// </summary>
internal partial class AppAssistantChatHistoryConfiguration : IEntityTypeConfiguration<AppAssistantChatHistoryEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAssistantChatHistoryEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_assistant_chat_history_pkey");

        entity.ToTable("app_assistant_chat_history", tb => tb.HasComment("对话历史，不保存实际历史记录"));

        entity.HasIndex(e => e.ChatId, "chat_history_pk_2");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ChatId)
            .HasComment("对话id")
            .HasColumnName("chat_id");
        entity.Property(e => e.CompletionsId)
            .HasMaxLength(50)
            .HasComment("对话id")
            .HasColumnName("completions_id");
        entity.Property(e => e.Content)
            .HasComment("内容")
            .HasColumnName("content");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Role)
            .HasMaxLength(20)
            .HasComment("角色")
            .HasColumnName("role");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAssistantChatHistoryEntity> modelBuilder);
}
