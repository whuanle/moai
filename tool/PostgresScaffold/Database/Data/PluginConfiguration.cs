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
/// 插件.
/// </summary>
internal partial class PluginConfiguration : IEntityTypeConfiguration<PluginEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65783_primary");

        entity.ToTable("plugin", tb => tb.HasComment("插件"));

        entity.HasIndex(e => e.PluginName, "idx_65783_plugin_plugin_name_index");

        entity.HasIndex(e => e.Title, "idx_65783_plugin_title_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ClassifyId)
            .HasDefaultValue(0)
            .HasComment("分类id")
            .HasColumnName("classify_id");
        entity.Property(e => e.Counter)
            .HasDefaultValue(0)
            .HasComment("计数器")
            .HasColumnName("counter");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("注释")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublic)
            .HasDefaultValue(false)
            .HasComment("公开访问")
            .HasColumnName("is_public");
        entity.Property(e => e.PluginId)
            .HasComment("对应的实际插件的id，不同类型的插件表不一样")
            .HasColumnName("plugin_id");
        entity.Property(e => e.PluginName)
            .HasMaxLength(50)
            .HasComment("插件名称")
            .HasColumnName("plugin_name");
        entity.Property(e => e.TeamId)
            .HasDefaultValue(0)
            .HasComment("某个团队创建的自定义插件")
            .HasColumnName("team_id");
        entity.Property(e => e.Title)
            .HasMaxLength(50)
            .HasComment("插件标题")
            .HasColumnName("title");
        entity.Property(e => e.Type)
            .HasComment("mcp|openapi|native|tool")
            .HasColumnName("type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginEntity> modelBuilder);
}
