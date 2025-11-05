using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 内置插件.
/// </summary>
public partial class PluginInternalConfiguration : IEntityTypeConfiguration<PluginInternalEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginInternalEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin_internal", tb => tb.HasComment("内置插件"));

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
        entity.Property(e => e.Config)
            .HasComment("配置参数")
            .HasColumnType("text")
            .HasColumnName("config");
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
        entity.Property(e => e.PluginName)
            .HasMaxLength(50)
            .HasComment("插件名称")
            .HasColumnName("plugin_name");
        entity.Property(e => e.TemplatePluginClassify)
            .HasMaxLength(20)
            .HasComment("对应的内置插件key")
            .HasColumnName("template_plugin_classify");
        entity.Property(e => e.TemplatePluginKey)
            .HasMaxLength(50)
            .HasComment("对应的内置插件key")
            .HasColumnName("template_plugin_key");
        entity.Property(e => e.Title)
            .HasMaxLength(50)
            .HasComment("插件标题")
            .HasColumnName("title");
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

    partial void OnConfigurePartial(EntityTypeBuilder<PluginInternalEntity> modelBuilder);
}
