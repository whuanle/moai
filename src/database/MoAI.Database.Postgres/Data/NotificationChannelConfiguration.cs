using System;
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
/// 通知中心渠道.
/// </summary>
internal partial class NotificationChannelConfiguration : IEntityTypeConfiguration<NotificationChannelEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<NotificationChannelEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_notification_channel_primary");

        entity.ToTable("notification_channel", tb => tb.HasComment("通知中心渠道：统一承接飞书/钉钉/企业微信/Telegram/通用 webhook/SMTP/站内推送等通知出口配置"));

        entity.HasIndex(e => e.Name, "idx_notification_channel_name_uindex")
            .IsUnique()
            .HasFilter("is_deleted = 0");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Config)
            .HasMaxLength(4000)
            .HasComment("渠道配置 JSON（按渠道类型结构化，含 webhook 地址/密钥/收件人等）")
            .HasColumnName("config");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(500)
            .HasDefaultValueSql("''::character varying")
            .HasComment("渠道描述")
            .HasColumnName("description");
        entity.Property(e => e.Enabled)
            .HasComment("是否启用")
            .HasColumnName("enabled");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(100)
            .HasComment("渠道名称（唯一）")
            .HasColumnName("name");
        entity.Property(e => e.Type)
            .HasComment("渠道类型")
            .HasColumnName("type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<NotificationChannelEntity> modelBuilder);
}
