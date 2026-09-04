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
/// 知识库文档，挂在知识库下的内容页.
/// </summary>
internal partial class WikiDocumentConfiguration : IEntityTypeConfiguration<WikiDocumentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_document_pkey");

        entity.ToTable("wiki_document", tb => tb.HasComment("知识库文档，挂在知识库下的内容页"));

        entity.HasIndex(e => e.WikiId, "idx_wiki_document_wiki_id");

        entity.Property(e => e.Id)
            .HasComment("文档ID，自增主键")
            .HasColumnName("id");
        entity.Property(e => e.Content)
            .HasDefaultValueSql("''::text")
            .HasComment("文档内容（Markdown，text 不限长）")
            .HasColumnName("content");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间，审计钩子自动填充，默认timezone(utc,now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人用户ID，审计钩子插入时自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasComment("文档标题，最长100字符")
            .HasColumnName("title");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间，审计钩子插入/更新/删除时自动刷新")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人用户ID，审计钩子更新/删除时自动填充")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("所属知识库ID，逻辑关联wiki.id（仓库约定不建物理外键）")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumentEntity> modelBuilder);
}
