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
public partial class WikiDocumentChunkMetadataPreviewConfiguration : IEntityTypeConfiguration<WikiDocumentChunkMetadataPreviewEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentChunkMetadataPreviewEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_document_chunk_metadata_preview", tb => tb.HasComment("切片元数据内容表（提问/提纲/摘要）"));

        entity.HasIndex(e => e.MetadataType, "idx_deriv_type");

        entity.HasIndex(e => new { e.DocumentId, e.MetadataType }, "idx_doc_deriv");

        entity.HasIndex(e => new { e.ChunkId, e.Id }, "idx_slice_deriv");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("bigint(20)")
            .HasColumnName("id");
        entity.Property(e => e.ChunkId)
            .HasComment("关联切片ID（表A主键）")
            .HasColumnType("bigint(20)")
            .HasColumnName("chunk_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DocumentId)
            .HasComment("关联文档唯一标识（冗余字段）")
            .HasColumnType("int(11)")
            .HasColumnName("document_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.MetadataContent)
            .HasComment("提问/提纲/摘要内容")
            .HasColumnType("text")
            .HasColumnName("metadata_content");
        entity.Property(e => e.MetadataType)
            .HasComment("元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段")
            .HasColumnType("int(11)")
            .HasColumnName("metadata_type");
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

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentChunkMetadataPreviewEntity> modelBuilder);
}
