using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识库任务.
/// </summary>
public partial class WikiDocumntTaskConfiguration : IEntityTypeConfiguration<WikiDocumntTaskEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiDocumntTaskEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_documnt_task", tb => tb.HasComment("知识库任务"));

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
        entity.Property(e => e.DocumentId)
            .HasComment("文档id")
            .HasColumnType("int(11)")
            .HasColumnName("document_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.MaxTokensPerParagraph)
            .HasDefaultValueSql("'1000'")
            .HasComment("每段最大token数量")
            .HasColumnType("int(11)")
            .HasColumnName("max_tokens_per_paragraph");
        entity.Property(e => e.Message)
            .HasComment("消息")
            .HasColumnType("text")
            .HasColumnName("message");
        entity.Property(e => e.OverlappingTokens)
            .HasDefaultValueSql("'20'")
            .HasComment("重叠的token数量")
            .HasColumnType("int(11)")
            .HasColumnName("overlapping_tokens");
        entity.Property(e => e.State)
            .HasComment("任务状态")
            .HasColumnType("int(11)")
            .HasColumnName("state");
        entity.Property(e => e.TaskTag)
            .HasMaxLength(50)
            .HasComment("任务标识")
            .HasColumnName("task_tag");
        entity.Property(e => e.Tokenizer)
            .HasMaxLength(20)
            .HasComment("分词器")
            .HasColumnName("tokenizer");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiDocumntTaskEntity> modelBuilder);
}
