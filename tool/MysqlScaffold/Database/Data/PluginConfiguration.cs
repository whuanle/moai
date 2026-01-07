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
/// 插件.
/// </summary>
public partial class PluginConfiguration : IEntityTypeConfiguration<PluginEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin", tb => tb.HasComment("插件"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.ClassifyId).HasComment("分类id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Description)
            .HasDefaultValueSql("''")
            .HasComment("注释");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsPublic).HasComment("公开访问");
        entity.Property(e => e.PluginId).HasComment("对应的实际插件的id，不同类型的插件表不一样");
        entity.Property(e => e.PluginName).HasComment("插件名称");
        entity.Property(e => e.TeamId).HasComment("某个团队创建的自定义插件");
        entity.Property(e => e.Title).HasComment("插件标题");
        entity.Property(e => e.Type).HasComment("mcp|openapi|native|tool");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginEntity> modelBuilder);
}
