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
/// 知识库文档.
/// </summary>
internal partial class WikiDocumentConfiguration : IEntityTypeConfiguration<WikiDocumentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_document_pkey");

        entity.ToTable("wiki_document", tb => tb.HasComment("知识库文档"));

        entity.HasIndex(e => e.ObjectKey, "wiki_document_object_key_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FileId)
            .HasComment("文件id")
            .HasColumnName("file_id");
        entity.Property(e => e.FileName)
            .HasMaxLength(1024)
            .HasComment("文档名称")
            .HasColumnName("file_name");
        entity.Property(e => e.FileType)
            .HasMaxLength(10)
            .HasComment("文件扩展名称，如.md")
            .HasColumnName("file_type");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsEmbedding)
            .HasComment("是否已经向量化")
            .HasColumnName("is_embedding");
        entity.Property(e => e.IsUpdate)
            .HasComment("是否有更新，需要重新进行向量化")
            .HasColumnName("is_update");
        entity.Property(e => e.ObjectKey)
            .HasMaxLength(100)
            .HasComment("文件路径")
            .HasColumnName("object_key");
        entity.Property(e => e.SliceConfig)
            .HasComment("切割配置")
            .HasColumnName("slice_config");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
            .HasComment("最后更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.VersionNo)
            .HasComment("版本号，可与向量元数据对比，确认最新文档版本号是否一致")
            .HasColumnName("version_no");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentEntity> modelBuilder);
}
