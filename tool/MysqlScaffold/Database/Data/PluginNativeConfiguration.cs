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
/// 内置插件.
/// </summary>
public partial class PluginNativeConfiguration : IEntityTypeConfiguration<PluginNativeEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginNativeEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin_native", tb => tb.HasComment("内置插件"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.Config)
            .HasDefaultValueSql("'{}'")
            .HasComment("配置参数");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.TemplatePluginClassify).HasComment("模板分类");
        entity.Property(e => e.TemplatePluginKey).HasComment("对应的内置插件key");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginNativeEntity> modelBuilder);
}
