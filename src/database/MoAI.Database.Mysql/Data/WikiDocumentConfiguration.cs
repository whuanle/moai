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
public partial class WikiDocumentConfiguration : IEntityTypeConfiguration<WikiDocumentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_document", tb => tb.HasComment("知识库文档"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FileId)
            .HasComment("文件id")
            .HasColumnType("int(11)")
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
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
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
            .HasDefaultValueSql("'{}'")
            .HasComment("切割配置")
            .HasColumnType("json")
            .HasColumnName("slice_config");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.VersionNo)
            .HasComment("版本号，可与向量元数据对比，确认最新文档版本号是否一致")
            .HasColumnType("bigint(20)")
            .HasColumnName("version_no");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentEntity> modelBuilder);
}
