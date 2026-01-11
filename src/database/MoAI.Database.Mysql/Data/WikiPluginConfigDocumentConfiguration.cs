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
/// 知识库文档关联任务，这里的任务都是成功的.
/// </summary>
public partial class WikiPluginConfigDocumentConfiguration : IEntityTypeConfiguration<WikiPluginConfigDocumentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginConfigDocumentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_plugin_config_document", tb => tb.HasComment("知识库文档关联任务，这里的任务都是成功的"));

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
        entity.Property(e => e.RelevanceKey)
            .HasMaxLength(1000)
            .HasComment("关联对象")
            .HasColumnName("relevance_key");
        entity.Property(e => e.RelevanceValue)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''")
            .HasComment("关联值")
            .HasColumnName("relevance_value");
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
        entity.Property(e => e.WikiDocumentId)
            .HasComment("文档id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_document_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginConfigDocumentEntity> modelBuilder);
}
