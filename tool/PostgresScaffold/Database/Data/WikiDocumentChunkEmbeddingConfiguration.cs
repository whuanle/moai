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
/// 切片向量化内容.
/// </summary>
internal partial class WikiDocumentChunkEmbeddingConfiguration : IEntityTypeConfiguration<WikiDocumentChunkEmbeddingEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentChunkEmbeddingEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_document_chunk_embedding_pkey");

        entity.ToTable("wiki_document_chunk_embedding", tb => tb.HasComment("切片向量化内容"));

        entity.HasIndex(e => e.MetadataType, "idx_deriv_type");

        entity.HasIndex(e => new { e.DocumentId, e.MetadataType }, "idx_doc_deriv");

        entity.HasIndex(e => e.Id, "idx_slice_deriv");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ChunkId)
            .HasComment("源id，关联自身")
            .HasColumnName("chunk_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
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
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsEmbedding)
            .HasComment("是否被向量化")
            .HasColumnName("is_embedding");
        entity.Property(e => e.MetadataContent)
            .HasComment("提问/提纲/摘要内容")
            .HasColumnName("metadata_content");
        entity.Property(e => e.MetadataType)
            .HasComment("元数据类型：0=原文，1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段")
            .HasColumnName("metadata_type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
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

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentChunkEmbeddingEntity> modelBuilder);
}
