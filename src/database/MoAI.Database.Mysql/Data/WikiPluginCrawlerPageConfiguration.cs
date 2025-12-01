using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 爬虫页面进度列表.
/// </summary>
public partial class WikiPluginCrawlerPageConfiguration : IEntityTypeConfiguration<WikiPluginCrawlerPageEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginCrawlerPageEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_plugin_crawler_page", tb => tb.HasComment("爬虫页面进度列表"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("bigint(20)")
            .HasColumnName("id");
        entity.Property(e => e.ConfigId)
            .HasComment("爬虫id")
            .HasColumnType("int(11)")
            .HasColumnName("config_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Message)
            .HasMaxLength(500)
            .HasDefaultValueSql("''")
            .HasComment("信息")
            .HasColumnName("message");
        entity.Property(e => e.State)
            .HasComment("爬取状态")
            .HasColumnType("int(11)")
            .HasColumnName("state");
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
        entity.Property(e => e.Url)
            .HasMaxLength(1000)
            .HasComment("正在爬取的地址")
            .HasColumnName("url");
        entity.Property(e => e.WikiDocumentId)
            .HasComment("文档id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_document_id");
        entity.Property(e => e.WikiId)
            .HasComment("知识库id")
            .HasColumnType("int(11)")
            .HasColumnName("wiki_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginCrawlerPageEntity> modelBuilder);
}
