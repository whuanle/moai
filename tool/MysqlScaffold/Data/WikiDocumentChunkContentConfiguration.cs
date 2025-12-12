using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 文档切片原始内容表.
/// </summary>
public partial class WikiDocumentChunkContentConfiguration : IEntityTypeConfiguration<WikiDocumentChunkContentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentChunkContentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_document_chunk_content", tb => tb.HasComment("文档切片原始内容表"));

        entity.HasIndex(e => new { e.DocumentId, e.Id }, "idx_doc_slice");

        entity.HasIndex(e => e.WikiId, "idx_wiki_slice");

        entity.Property(e => e.Id)
            .HasComment("切片唯一ID（slice_id）")
            .HasColumnType("bigint(20)")
            .HasColumnName("id");
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
            .HasComment("关联文档唯一标识")
            .HasColumnType("int(11)")
            .HasColumnName("document_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.SliceContent)
            .HasComment("原始切片内容")
            .HasColumnType("text")
            .HasColumnName("slice_content");
        entity.Property(e => e.SliceLength)
            .HasComment("切片字符长度")
            .HasColumnType("int(11)")
            .HasColumnName("slice_length");
        entity.Property(e => e.SliceOrder)
            .HasComment("切片在文档中的顺序")
            .HasColumnType("int(11)")
            .HasColumnName("slice_order");
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
            .HasComment("关联知识库标识（冗余字段）")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentChunkContentEntity> modelBuilder);
}
