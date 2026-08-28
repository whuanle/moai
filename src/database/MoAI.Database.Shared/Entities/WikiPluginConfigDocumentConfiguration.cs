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
internal partial class WikiPluginConfigDocumentConfiguration : IEntityTypeConfiguration<WikiPluginConfigDocumentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginConfigDocumentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_66020_primary");

        entity.ToTable("wiki_plugin_config_document", tb => tb.HasComment("知识库文档关联任务，这里的任务都是成功的"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ConfigId)
            .HasComment("爬虫id")
            .HasColumnName("config_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.RelevanceKey)
            .HasMaxLength(1000)
            .HasComment("关联对象")
            .HasColumnName("relevance_key");
        entity.Property(e => e.RelevanceValue)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("关联值")
            .HasColumnName("relevance_value");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiDocumentId)
            .HasDefaultValue(0)
            .HasComment("文档id")
            .HasColumnName("wiki_document_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginConfigDocumentEntity> modelBuilder);
}
