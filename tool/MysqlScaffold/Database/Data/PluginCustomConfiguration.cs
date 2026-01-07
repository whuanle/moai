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
/// 自定义插件.
/// </summary>
public partial class PluginCustomConfiguration : IEntityTypeConfiguration<PluginCustomEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginCustomEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("plugin_custom", tb => tb.HasComment("自定义插件"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Headers).HasComment("头部");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.OpenapiFileId).HasComment("文件id");
        entity.Property(e => e.OpenapiFileName).HasComment("文件名称");
        entity.Property(e => e.Queries).HasComment("query参数");
        entity.Property(e => e.Server).HasComment("服务器地址");
        entity.Property(e => e.Type).HasComment("mcp|openapi");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PluginCustomEntity> modelBuilder);
}
