using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 对话历史，不保存实际历史记录.
/// </summary>
public partial class AppAssistantChatHistoryConfiguration : IEntityTypeConfiguration<AppAssistantChatHistoryEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAssistantChatHistoryEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_assistant_chat_history", tb => tb.HasComment("对话历史，不保存实际历史记录"));

        entity.HasIndex(e => e.ChatId, "chat_history_pk_2").IsUnique();

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("bigint(20)")
            .HasColumnName("id");
        entity.Property(e => e.ChatId)
            .HasMaxLength(16)
            .IsFixedLength()
            .HasComment("对话id")
            .HasColumnName("chat_id");
        entity.Property(e => e.CompletionsId)
            .HasMaxLength(20)
            .HasComment("对话id")
            .HasColumnName("completions_id");
        entity.Property(e => e.Content)
            .HasComment("内容")
            .HasColumnType("text")
            .HasColumnName("content");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Model)
            .HasMaxLength(20)
            .HasComment("模型名称")
            .HasColumnName("model");
        entity.Property(e => e.Role)
            .HasMaxLength(20)
            .HasComment("角色")
            .HasColumnName("role");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAssistantChatHistoryEntity> modelBuilder);
}
