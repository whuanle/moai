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
/// 知识库插件配置.
/// </summary>
public partial class WikiPluginConfigConfiguration : IEntityTypeConfiguration<WikiPluginConfigEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WikiPluginConfigEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("wiki_plugin_config", tb => tb.HasComment("知识库插件配置"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.Config)
            .HasDefaultValueSql("'{}'")
            .HasComment("配置");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.PluginType).HasComment("插件类型");
        entity.Property(e => e.Title).HasComment("插件标题");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("更新人");
        entity.Property(e => e.WikiId).HasComment("知识库id");
        entity.Property(e => e.WorkMessage)
            .HasDefaultValueSql("''")
            .HasComment("运行信息");
        entity.Property(e => e.WorkState).HasComment("状态");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WikiPluginConfigEntity> modelBuilder);
}
