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
        entity.HasKey(e => e.Id).HasName("app_assistant_chat_pkey");

        entity.ToTable("app_assistant_chat", tb => tb.HasComment("ai助手表"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Avatar)
            .HasMaxLength(10)
            .HasComment("头像")
            .HasColumnName("avatar");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.ExecutionSettings)
            .HasComment("对话影响参数")
            .HasColumnName("execution_settings");
        entity.Property(e => e.InputTokens)
            .HasComment("输入token统计")
            .HasColumnName("input_tokens");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ModelId)
            .HasComment("对话使用的模型 id")
            .HasColumnName("model_id");
        entity.Property(e => e.OutTokens)
            .HasComment("输出token统计")
            .HasColumnName("out_tokens");
        entity.Property(e => e.Plugins)
            .HasComment("要使用的插件")
            .HasColumnName("plugins");
        entity.Property(e => e.Prompt)
            .HasMaxLength(4000)
            .HasComment("提示词")
            .HasColumnName("prompt");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasComment("对话标题")
            .HasColumnName("title");
        entity.Property(e => e.TotalTokens)
            .HasComment("使用的 token 总数")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiIds)
            .HasComment("要使用的知识库id")
            .HasColumnName("wiki_ids");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAssistantChatEntity> modelBuilder);
}
