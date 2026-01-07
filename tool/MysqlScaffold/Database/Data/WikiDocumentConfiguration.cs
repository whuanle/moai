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

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.FileId).HasComment("文件id");
        entity.Property(e => e.FileName).HasComment("文档名称");
        entity.Property(e => e.FileType).HasComment("文件扩展名称，如.md");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsEmbedding).HasComment("是否已经向量化");
        entity.Property(e => e.IsUpdate).HasComment("是否有更新，需要重新进行向量化");
        entity.Property(e => e.ObjectKey).HasComment("文件路径");
        entity.Property(e => e.SliceConfig)
            .HasDefaultValueSql("'{}'")
            .HasComment("切割配置");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");
        entity.Property(e => e.VersionNo).HasComment("版本号，可与向量元数据对比，确认最新文档版本号是否一致");
        entity.Property(e => e.WikiId).HasComment("知识库id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentEntity> modelBuilder);
}
