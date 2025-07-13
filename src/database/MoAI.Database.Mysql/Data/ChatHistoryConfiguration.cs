using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 对话历史.
/// </summary>
public partial class ChatHistoryConfiguration : IEntityTypeConfiguration<ChatHistoryEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ChatHistoryEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("chat_history", tb => tb.HasComment("对话历史"));

        entity.HasIndex(e => e.ChatId, "chat_history_pk_2").IsUnique();

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.ChatId)
            .HasComment("对话id")
            .HasColumnType("bigint(20)")
            .HasColumnName("chat_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.ExecutionSettings)
            .HasMaxLength(1000)
            .HasDefaultValueSql("'{}'")
            .HasComment("对话属性")
            .HasColumnName("execution_settings");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ModelId)
            .HasComment("模型id")
            .HasColumnType("int(11)")
            .HasColumnName("model_id");
        entity.Property(e => e.PluginIds)
            .HasMaxLength(1000)
            .HasDefaultValueSql("'[]'")
            .HasComment("插件列表")
            .HasColumnName("plugin_ids");
        entity.Property(e => e.Prompt)
            .HasMaxLength(2000)
            .HasDefaultValueSql("''")
            .HasComment("提示词")
            .HasColumnName("prompt");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasComment("话题标题")
            .HasColumnName("title");
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
        entity.Property(e => e.WikiId)
            .HasComment("使用的知识库id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ChatHistoryEntity> modelBuilder);
}
