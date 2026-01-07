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
public partial class AppAssistantChatConfiguration : IEntityTypeConfiguration<AppAssistantChatEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAssistantChatEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_assistant_chat", tb => tb.HasComment("ai助手表"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id");
        entity.Property(e => e.Avatar)
            .HasDefaultValueSql("'?'")
            .HasComment("头像");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.ExecutionSettings)
            .HasDefaultValueSql("'[]'")
            .HasComment("对话影响参数");
        entity.Property(e => e.InputTokens).HasComment("输入token统计");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.ModelId).HasComment("对话使用的模型 id");
        entity.Property(e => e.OutTokens).HasComment("输出token统计");
        entity.Property(e => e.Plugins)
            .HasDefaultValueSql("'[]'")
            .HasComment("要使用的插件");
        entity.Property(e => e.Prompt)
            .HasDefaultValueSql("''")
            .HasComment("提示词");
        entity.Property(e => e.Title)
            .HasDefaultValueSql("'未命名标题'")
            .HasComment("对话标题");
        entity.Property(e => e.TotalTokens).HasComment("使用的 token 总数");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.WikiIds)
            .HasDefaultValueSql("'0'")
            .HasComment("要使用的知识库id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAssistantChatEntity> modelBuilder);
}
