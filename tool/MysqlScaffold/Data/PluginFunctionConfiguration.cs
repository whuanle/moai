using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 插件函数.
/// </summary>
public partial class PluginFunctionConfiguration : IEntityTypeConfiguration<PluginFunctionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginFunctionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin_function", tb => tb.HasComment("插件函数"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
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
        entity.Property(e => e.Name)
            .HasMaxLength(255)
            .HasComment("函数名称")
            .HasColumnName("name");
        entity.Property(e => e.Path)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("api路径")
            .HasColumnName("path");
        entity.Property(e => e.PluginId)
            .HasComment("插件路径")
            .HasColumnType("int(11)")
            .HasColumnName("plugin_id");
        entity.Property(e => e.Summary)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''")
            .HasComment("描述")
            .HasColumnName("summary");
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

    partial void OnConfigurePartial(EntityTypeBuilder<PluginFunctionEntity> modelBuilder);
}
