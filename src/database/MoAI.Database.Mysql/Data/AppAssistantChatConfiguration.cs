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
/// ai助手表.
/// </summary>
internal partial class AppAssistantChatConfiguration : IEntityTypeConfiguration<AppAssistantChatEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAssistantChatEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_assistant_chat", tb => tb.HasComment("ai助手表"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Avatar)
            .HasMaxLength(10)
            .HasDefaultValueSql("'?'")
            .HasComment("头像")
            .HasColumnName("avatar");
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
            .HasDefaultValueSql("'[]'")
            .HasComment("对话影响参数")
            .HasColumnName("execution_settings")
            .UseCollation("utf8mb4_bin");
        entity.Property(e => e.InputTokens)
            .HasComment("输入token统计")
            .HasColumnType("int(11)")
            .HasColumnName("input_tokens");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ModelId)
            .HasComment("对话使用的模型 id")
            .HasColumnType("int(11)")
            .HasColumnName("model_id");
        entity.Property(e => e.OutTokens)
            .HasComment("输出token统计")
            .HasColumnType("int(11)")
            .HasColumnName("out_tokens");
        entity.Property(e => e.Plugins)
            .HasDefaultValueSql("'[]'")
            .HasComment("要使用的插件")
            .HasColumnName("plugins")
            .UseCollation("utf8mb4_bin");
        entity.Property(e => e.Prompt)
            .HasMaxLength(4000)
            .HasDefaultValueSql("''")
            .HasComment("提示词")
            .HasColumnName("prompt");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasDefaultValueSql("'未命名标题'")
            .HasComment("对话标题")
            .HasColumnName("title");
        entity.Property(e => e.TotalTokens)
            .HasComment("使用的 token 总数")
            .HasColumnType("int(11)")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiIds)
            .HasDefaultValueSql("'0'")
            .HasComment("要使用的知识库id")
            .HasColumnName("wiki_ids")
            .UseCollation("utf8mb4_bin");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAssistantChatEntity> modelBuilder);
}
