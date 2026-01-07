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
public partial class WikiDocumentChunkEmbeddingConfiguration : IEntityTypeConfiguration<WikiDocumentChunkEmbeddingEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentChunkEmbeddingEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_document_chunk_embedding", tb => tb.HasComment("切片向量化内容"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("元数据内容唯一ID（derivative_id）");
        entity.Property(e => e.ChunkId).HasComment("源id，关联自身");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.DerivativeContent).HasComment("提问/提纲/摘要内容");
        entity.Property(e => e.DerivativeType).HasComment("元数据类型：0=原文，1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段");
        entity.Property(e => e.DocumentId).HasComment("关联文档唯一标识（冗余字段）");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsEmbedding).HasComment("是否被向量化");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.WikiId).HasComment("知识库id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentChunkEmbeddingEntity> modelBuilder);
}
