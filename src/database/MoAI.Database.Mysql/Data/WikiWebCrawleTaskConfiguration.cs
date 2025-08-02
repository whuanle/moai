using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// web爬虫状态.
/// </summary>
public partial class WikiWebCrawleTaskConfiguration : IEntityTypeConfiguration<WikiWebCrawleTaskEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiWebCrawleTaskEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_web_crawle_task", tb => tb.HasComment("web爬虫状态"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CrawleState)
            .HasComment("爬取状态")
            .HasColumnType("int(11)")
            .HasColumnName("crawle_state");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FaildPageCount)
            .HasComment("爬取失败的页面数量")
            .HasColumnType("int(11)")
            .HasColumnName("faild_page_count");
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
            .HasMaxLength(255)
            .HasComment("任务执行信息")
            .HasColumnName("message");

        entity.Property(e => e.OverlappingTokens)
            .HasDefaultValueSql("'20'")
            .HasComment("重叠的token数量")
            .HasColumnType("int(11)")
            .HasColumnName("overlapping_tokens");
        entity.Property(e => e.PageCount)
            .HasComment("爬取成功的页面数量")
            .HasColumnType("int(11)")
            .HasColumnName("page_count");
        entity.Property(e => e.Tokenizer)
            .HasMaxLength(20)
            .HasComment("分词器")
            .HasColumnName("tokenizer");
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
            .HasComment("知识库id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");
        entity.Property(e => e.WikiWebConfigId)
            .HasComment("web配置id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_web_config_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiWebCrawleTaskEntity> modelBuilder);
}
