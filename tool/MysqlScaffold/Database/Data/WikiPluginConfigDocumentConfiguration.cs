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
/// 知识库文档关联任务，这里的任务都是成功的.
/// </summary>
public partial class WikiPluginConfigDocumentConfiguration : IEntityTypeConfiguration<WikiPluginConfigDocumentEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginConfigDocumentEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_plugin_config_document", tb => tb.HasComment("知识库文档关联任务，这里的任务都是成功的"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.ConfigId).HasComment("爬虫id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.RelevanceKey).HasComment("关联对象");
        entity.Property(e => e.RelevanceValue)
            .HasDefaultValueSql("''")
            .HasComment("关联值");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.WikiDocumentId).HasComment("文档id");
        entity.Property(e => e.WikiId).HasComment("知识库id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginConfigDocumentEntity> modelBuilder);
}
