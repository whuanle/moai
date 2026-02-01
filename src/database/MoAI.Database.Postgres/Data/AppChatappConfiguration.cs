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
/// 普通应用.
/// </summary>
internal partial class AppChatappConfiguration : IEntityTypeConfiguration<AppChatappEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppChatappEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_chatapp_pkey");

        entity.ToTable("app_chatapp", tb => tb.HasComment("普通应用"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("所属应用id")
            .HasColumnName("app_id");
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
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ModelId)
            .HasComment("对话使用的模型 id")
            .HasColumnName("model_id");
        entity.Property(e => e.Plugins)
            .HasComment("要使用的插件")
            .HasColumnName("plugins");
        entity.Property(e => e.Prompt)
            .HasMaxLength(4000)
            .HasComment("提示词")
            .HasColumnName("prompt");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
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

    partial void OnConfigurePartial(EntityTypeBuilder<AppChatappEntity> modelBuilder);
}
