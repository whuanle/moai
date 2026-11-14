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
/// 资源使用统计（每日）.
/// </summary>
internal partial class ResourceUsageDailyConfiguration : IEntityTypeConfiguration<ResourceUsageDailyEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ResourceUsageDailyEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_resource_usage_daily_primary");

        entity.ToTable("resource_usage_daily", tb => tb.HasComment("资源使用统计（每日），统计模块定期批量落库"));

        // 同一 (日期, 小时, 资源类型, 资源id) 只允许一行，落库走 upsert 累加；usage_hour=-1 表示按天粒度
        entity.HasIndex(e => new { e.UsageDate, e.UsageHour, e.Name, e.ResourceId }, "idx_resource_usage_daily_date_hour_name_resource_uindex")
            .IsUnique();

        entity.HasIndex(e => new { e.Name, e.UsageDate }, "idx_resource_usage_daily_name_date_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Count)
            .HasComment("当日累计数量")
            .HasColumnName("count");
        entity.Property(e => e.UsageHour)
            .HasDefaultValueSql("(-1)")
            .HasComment("统计小时（UTC，0~23）；-1 表示按天粒度（整日一个桶）")
            .HasColumnName("usage_hour");
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
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UsageDate)
            .HasComment("统计日期（UTC 日）")
            .HasColumnName("usage_date");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ResourceUsageDailyEntity> modelBuilder);
}
