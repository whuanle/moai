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
/// 知识库文档内嵌图片附件.
/// </summary>
internal partial class WikiDocumentImageConfiguration : IEntityTypeConfiguration<WikiDocumentImageEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentImageEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_wiki_document_image_primary");

        entity.ToTable("wiki_document_image", tb => tb.HasComment("知识库文档内嵌图片附件，文档提取时落出的图片资产登记"));

        entity.HasIndex(e => new { e.WikiId, e.DocumentId }, "idx_wiki_document_image_document_id_index")
            .HasFilter("\"is_deleted\" = 0");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DocumentId)
            .HasComment("知识库文档id（wiki_document.id，逻辑关联，仓库约定不建物理外键）")
            .HasColumnName("document_id");
        entity.Property(e => e.FileId)
            .HasComment("文件id（file.id，图片本体在对象存储，按内容寻址可被多文档共享）")
            .HasColumnName("file_id");
        entity.Property(e => e.FileName)
            .HasMaxLength(1024)
            .HasComment("图片文件名，取自文档内部部件名或提取序名")
            .HasColumnName("file_name");
        entity.Property(e => e.FileType)
            .HasMaxLength(10)
            .HasComment("文件扩展名称，如.png")
            .HasColumnName("file_type");
        entity.Property(e => e.ImageIndex)
            .HasDefaultValueSql("'0'::integer")
            .HasComment("文档内序号，按提取产出顺序从 0 递增")
            .HasColumnName("image_index");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ObjectKey)
            .HasMaxLength(255)
            .HasComment("文件路径，wiki/{wikiId}/image/{sha256}.{ext}")
            .HasColumnName("object_key");
        entity.Property(e => e.PageNumber)
            .HasComment("所在页码（pdf 提取可解析），null=未知")
            .HasColumnName("page_number");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentImageEntity> modelBuilder);
}
