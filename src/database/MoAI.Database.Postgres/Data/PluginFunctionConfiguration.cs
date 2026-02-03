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
/// 插件函数.
/// </summary>
internal partial class PluginFunctionConfiguration : IEntityTypeConfiguration<PluginFunctionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginFunctionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_63215_primary");

        entity.ToTable("plugin_function", tb => tb.HasComment("插件函数"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(255)
            .HasComment("函数名称")
            .HasColumnName("name");
        entity.Property(e => e.Path)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("api路径")
            .HasColumnName("path");
        entity.Property(e => e.PluginCustomId)
            .HasComment("plugin_custom_id")
            .HasColumnName("plugin_custom_id");
        entity.Property(e => e.Summary)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("描述")
            .HasColumnName("summary");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginFunctionEntity> modelBuilder);
}
