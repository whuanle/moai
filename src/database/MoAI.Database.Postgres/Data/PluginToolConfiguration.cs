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
internal partial class PluginToolConfiguration : IEntityTypeConfiguration<PluginToolEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginToolEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("plugin_tool_pkey");

        entity.ToTable("plugin_tool", tb => tb.HasComment("内置插件"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.TemplatePluginClassify)
            .HasMaxLength(20)
            .HasComment("模板分类")
            .HasColumnName("template_plugin_classify");
        entity.Property(e => e.TemplatePluginKey)
            .HasMaxLength(50)
            .HasComment("对应的内置插件key")
            .HasColumnName("template_plugin_key");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
            .HasComment("最后更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginToolEntity> modelBuilder);
}
