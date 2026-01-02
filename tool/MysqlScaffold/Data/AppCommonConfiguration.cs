using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 普通应用.
/// </summary>
public partial class AppCommonConfiguration : IEntityTypeConfiguration<AppCommonEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppCommonEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app_common", tb => tb.HasComment("普通应用"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("所属应用id")
            .HasColumnName("app_id");
        entity.Property(e => e.Avatar)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("AI头像,存 objectKey")
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
            .HasColumnType("json")
            .HasColumnName("execution_settings");
        entity.Property(e => e.IsAuth)
            .HasComment("是否开启授权验证")
            .HasColumnName("is_auth");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ModelId)
            .HasComment("对话使用的模型 id")
            .HasColumnType("int(11)")
            .HasColumnName("model_id");
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
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
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
        entity.Property(e => e.WikiIds)
            .HasDefaultValueSql("'0'")
            .HasComment("要使用的知识库id")
            .HasColumnType("json")
            .HasColumnName("wiki_ids");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppCommonEntity> modelBuilder);
}
