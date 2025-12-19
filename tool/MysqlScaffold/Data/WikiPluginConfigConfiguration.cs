using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识库插件配置.
/// </summary>
public partial class WikiPluginConfigConfiguration : IEntityTypeConfiguration<WikiPluginConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_plugin_config", tb => tb.HasComment("知识库插件配置"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.Config)
            .HasDefaultValueSql("'{}'")
            .HasComment("配置")
            .HasColumnType("json")
            .HasColumnName("config");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
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
        entity.Property(e => e.PluginType)
            .HasMaxLength(10)
            .HasComment("插件类型")
            .HasColumnName("plugin_type");
        entity.Property(e => e.Title)
            .HasMaxLength(20)
            .HasComment("插件标题")
            .HasColumnName("title");
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
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");
        entity.Property(e => e.WorkMessage)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''")
            .HasComment("运行信息")
            .HasColumnName("work_message");
        entity.Property(e => e.WorkState)
            .HasComment("状态")
            .HasColumnType("int(11)")
            .HasColumnName("work_state");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginConfigEntity> modelBuilder);
}
