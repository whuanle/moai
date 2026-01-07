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
public partial class PluginFunctionConfiguration : IEntityTypeConfiguration<PluginFunctionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginFunctionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin_function", tb => tb.HasComment("插件函数"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.Name).HasComment("函数名称");
        entity.Property(e => e.Path)
            .HasDefaultValueSql("''")
            .HasComment("api路径");
        entity.Property(e => e.PluginCustomId).HasComment("plugin_custom_id");
        entity.Property(e => e.Summary)
            .HasDefaultValueSql("''")
            .HasComment("描述");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginFunctionEntity> modelBuilder);
}
