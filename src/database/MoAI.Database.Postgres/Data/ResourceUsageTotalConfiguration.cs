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
/// 资源使用统计（总量）.
/// </summary>
internal partial class ResourceUsageTotalConfiguration : IEntityTypeConfiguration<ResourceUsageTotalEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ResourceUsageTotalEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_resource_usage_total_primary");

        entity.ToTable("resource_usage_total", tb => tb.HasComment("资源使用统计（总量），统计模块定期批量落库，不按日隔离"));

        // 同一 (资源类型, 资源id) 只允许一行，落库走 upsert 累加
        entity.HasIndex(e => new { e.Name, e.ResourceId }, "idx_resource_usage_total_name_resource_uindex")
            .IsUnique();

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.Name)
            .HasMaxLength(100)
            .HasComment("资源类型名，如 plugin-usage / app-usage / skill-usage")
            .HasColumnName("name");
        entity.Property(e => e.ResourceId)
            .HasMaxLength(128)
            .HasComment("资源 id 文本，各资源主键形态不一（Guid/int/key），统一以字符串存储")
            .HasColumnName("resource_id");
        entity.Property(e => e.Total)
            .HasComment("累计总量")
            .HasColumnName("total");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ResourceUsageTotalEntity> modelBuilder);
}
