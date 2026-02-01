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
/// 文档切片预览.
/// </summary>
internal partial class WikiDocumentChunkContentPreviewConfiguration : IEntityTypeConfiguration<WikiDocumentChunkContentPreviewEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentChunkContentPreviewEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_document_chunk_content_preview_pkey");

        entity.ToTable("wiki_document_chunk_content_preview", tb => tb.HasComment("文档切片预览"));

        entity.HasIndex(e => new { e.DocumentId, e.Id }, "idx_doc_slice");

        entity.HasIndex(e => e.WikiId, "idx_wiki_slice");

        entity.Property(e => e.Id)
            .HasComment("切片唯一ID（slice_id）")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DocumentId)
            .HasComment("关联文档唯一标识")
            .HasColumnName("document_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.SliceContent)
            .HasComment("原始切片内容")
            .HasColumnName("slice_content");
        entity.Property(e => e.SliceLength)
            .HasComment("切片字符长度")
            .HasColumnName("slice_length");
        entity.Property(e => e.SliceOrder)
            .HasComment("切片在文档中的顺序")
            .HasColumnName("slice_order");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("关联知识库标识（冗余字段）")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentChunkContentPreviewEntity> modelBuilder);
}
