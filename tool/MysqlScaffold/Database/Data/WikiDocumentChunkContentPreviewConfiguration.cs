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
public partial class WikiDocumentChunkContentPreviewConfiguration : IEntityTypeConfiguration<WikiDocumentChunkContentPreviewEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentChunkContentPreviewEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_document_chunk_content_preview", tb => tb.HasComment("文档切片预览"));

        entity.Property(e => e.Id).HasComment("切片唯一ID（slice_id）");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.DocumentId).HasComment("关联文档唯一标识");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.SliceContent).HasComment("原始切片内容");
        entity.Property(e => e.SliceLength).HasComment("切片字符长度");
        entity.Property(e => e.SliceOrder).HasComment("切片在文档中的顺序");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.WikiId).HasComment("关联知识库标识（冗余字段）");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentChunkContentPreviewEntity> modelBuilder);
}
