using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// wiki网页抓取.
/// </summary>
public partial class WikiWebConfigConfiguration : IEntityTypeConfiguration<WikiWebConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiWebConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_web_config", tb => tb.HasComment("wiki网页抓取"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.Address)
            .HasMaxLength(255)
            .HasComment("页面地址")
            .HasColumnName("address");
        entity.Property(e => e.CrawlSchame)
            .HasComment("抓取方式")
            .HasColumnType("int(11)")
            .HasColumnName("crawl_schame");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsAutoEmbedding)
            .HasComment("是否自动向量化")
            .HasColumnName("is_auto_embedding");
        entity.Property(e => e.IsCrawlOther)
            .HasComment("是否抓取其它页面")
            .HasColumnName("is_crawl_other");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LimitPath)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("限制自动爬取的网页都在该路径之下")
            .HasColumnName("limit_path");
        entity.Property(e => e.SamePathRule)
            .HasComment("抓取的页面在以前抓取过时怎么处理")
            .HasColumnType("int(11)")
            .HasColumnName("same_path_rule");
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

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiWebConfigEntity> modelBuilder);
}
