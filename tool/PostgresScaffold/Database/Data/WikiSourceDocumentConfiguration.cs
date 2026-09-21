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
/// 外部源文档，外部源条目与知识库文档的映射，记录内容哈希用于增量比对.
/// </summary>
internal partial class WikiSourceDocumentConfiguration : IEntityTypeConfiguration<WikiSourceDocumentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiSourceDocumentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("wiki_source_document_pkey");

        entity.ToTable("wiki_source_document", tb => tb.HasComment("外部源文档，外部源条目与知识库文档的映射，记录内容哈希用于增量比对"));

        entity.HasIndex(e => new { e.SourceId, e.ExternalDocToken }, "idx_wiki_source_document_doc_token_index");

        entity.HasIndex(e => e.DocumentId, "idx_wiki_source_document_document_id_index");

        entity.HasIndex(e => new { e.SourceId, e.ExternalKey }, "idx_wiki_source_document_key_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.SourceId, "idx_wiki_source_document_source_id_index");

        entity.HasIndex(e => e.WikiId, "idx_wiki_source_document_wiki_id_index");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.ContentHash)
            .HasMaxLength(64)
            .HasDefaultValueSql("''::character varying")
            .HasComment("最近一次同步内容的 SHA-256，用于判断是否变化")
            .HasColumnName("content_hash");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DocumentId)
            .HasComment("知识库文档id（wiki_document.id，逻辑关联）")
            .HasColumnName("document_id");
        entity.Property(e => e.ExternalDocToken)
            .HasMaxLength(128)
            .HasDefaultValueSql("''::character varying")
            .HasComment("外部文档内容标识，飞书文档源为文档token（obj_token），云文档变更事件按此匹配")
            .HasColumnName("external_doc_token");
        entity.Property(e => e.ExternalKey)
            .HasMaxLength(128)
            .HasComment("外部文档唯一标识，飞书文档源为节点token")
            .HasColumnName("external_key");
        entity.Property(e => e.ExternalPath)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("外部文档在源中的路径（面包屑，斜杠分隔）")
            .HasColumnName("external_path");
        entity.Property(e => e.ExternalTitle)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("外部文档标题")
            .HasColumnName("external_title");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LastError)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("最近一次同步错误信息")
            .HasColumnName("last_error");
        entity.Property(e => e.LastSyncTime)
            .HasComment("最近一次同步时间")
            .HasColumnName("last_sync_time");
        entity.Property(e => e.Revision)
            .HasMaxLength(64)
            .HasDefaultValueSql("''::character varying")
            .HasComment("外部文档版本号（飞书为文档编辑时间/版本号），辅助判断变化")
            .HasColumnName("revision");
        entity.Property(e => e.SourceId)
            .HasComment("外部源id（wiki_source.id，逻辑关联，仓库约定不建物理外键）")
            .HasColumnName("source_id");
        entity.Property(e => e.Status)
            .HasComment("同步状态，见 WikiSourceDocumentStatus（0=待同步，1=已同步，2=失败）")
            .HasColumnName("status");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiSourceDocumentEntity> modelBuilder);
}
