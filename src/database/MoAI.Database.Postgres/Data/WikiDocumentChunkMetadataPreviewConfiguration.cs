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
/// 切片元数据内容表（提问/提纲/摘要）.
/// </summary>
internal partial class WikiDocumentChunkMetadataPreviewConfiguration : IEntityTypeConfiguration<WikiDocumentChunkMetadataPreviewEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentChunkMetadataPreviewEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_63389_primary");

        entity.ToTable("wiki_document_chunk_metadata_preview", tb => tb.HasComment("切片元数据内容表（提问/提纲/摘要）"));

        entity.HasIndex(e => e.MetadataType, "idx_63389_idx_deriv_type");

        entity.HasIndex(e => new { e.DocumentId, e.MetadataType }, "idx_63389_idx_doc_deriv");

        entity.HasIndex(e => new { e.ChunkId, e.Id }, "idx_63389_idx_slice_deriv");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ChunkId)
            .HasComment("关联切片ID（表A主键）")
            .HasColumnName("chunk_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DocumentId)
            .HasComment("关联文档唯一标识（冗余字段）")
            .HasColumnName("document_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.MetadataContent)
            .HasComment("提问/提纲/摘要内容")
            .HasColumnName("metadata_content");
        entity.Property(e => e.MetadataType)
            .HasComment("元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段")
            .HasColumnName("metadata_type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentChunkMetadataPreviewEntity> modelBuilder);
}
