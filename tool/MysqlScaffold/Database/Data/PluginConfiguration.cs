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
public partial class PluginConfiguration : IEntityTypeConfiguration<PluginEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin", tb => tb.HasComment("插件"));

        entity.HasIndex(e => e.PluginName, "plugin_plugin_name_index");

        entity.HasIndex(e => e.Title, "plugin_title_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.ClassifyId)
            .HasComment("分类id")
            .HasColumnType("int(11)")
            .HasColumnName("classify_id");
        entity.Property(e => e.Counter)
            .HasComment("计数器")
            .HasColumnType("int(11)")
            .HasColumnName("counter");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("注释")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsPublic)
            .HasComment("公开访问")
            .HasColumnName("is_public");
        entity.Property(e => e.PluginId)
            .HasComment("对应的实际插件的id，不同类型的插件表不一样")
            .HasColumnType("int(11)")
            .HasColumnName("plugin_id");
        entity.Property(e => e.PluginName)
            .HasMaxLength(50)
            .HasComment("插件名称")
            .HasColumnName("plugin_name");
        entity.Property(e => e.TeamId)
            .HasComment("某个团队创建的自定义插件")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
        entity.Property(e => e.Title)
            .HasMaxLength(50)
            .HasComment("插件标题")
            .HasColumnName("title");
        entity.Property(e => e.Type)
            .HasComment("mcp|openapi|native|tool")
            .HasColumnType("int(11)")
            .HasColumnName("type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginEntity> modelBuilder);
}
