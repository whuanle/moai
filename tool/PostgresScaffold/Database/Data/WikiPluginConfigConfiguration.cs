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
/// 知识库插件配置.
/// </summary>
internal partial class WikiPluginConfigConfiguration : IEntityTypeConfiguration<WikiPluginConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_66005_primary");

        entity.ToTable("wiki_plugin_config", tb => tb.HasComment("知识库插件配置"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Config)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("配置")
            .HasColumnName("config");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.PluginType)
            .HasMaxLength(10)
            .HasComment("插件类型")
            .HasColumnName("plugin_type");
        entity.Property(e => e.Title)
            .HasMaxLength(20)
            .HasComment("插件标题")
            .HasColumnName("title");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");
        entity.Property(e => e.WorkMessage)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("运行信息")
            .HasColumnName("work_message");
        entity.Property(e => e.WorkState)
            .HasDefaultValue(0)
            .HasComment("状态")
            .HasColumnName("work_state");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginConfigEntity> modelBuilder);
}
