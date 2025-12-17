using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识库文档关联任务，这里的任务都是成功的.
/// </summary>
public partial class WikiPluginConfigDocumentStateConfiguration : IEntityTypeConfiguration<WikiPluginConfigDocumentStateEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginConfigDocumentStateEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_plugin_config_document_state", tb => tb.HasComment("知识库文档关联任务，这里的任务都是成功的"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.ConfigId)
            .HasComment("爬虫id")
            .HasColumnType("int(11)")
            .HasColumnName("config_id");
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
        entity.Property(e => e.Message)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''")
            .HasComment("信息")
            .HasColumnName("message");
        entity.Property(e => e.RelevanceKey)
            .HasMaxLength(1000)
            .HasComment("关联对象")
            .HasColumnName("relevance_key");
        entity.Property(e => e.RelevanceValue)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''")
            .HasComment("关联值")
            .HasColumnName("relevance_value");
        entity.Property(e => e.State)
            .HasComment("状态")
            .HasColumnType("int(11)")
            .HasColumnName("state");
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

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginConfigDocumentStateEntity> modelBuilder);
}
