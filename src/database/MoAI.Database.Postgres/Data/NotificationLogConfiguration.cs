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
/// 通知发送记录.
/// </summary>
internal partial class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLogEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<NotificationLogEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_notification_log_primary");

        entity.ToTable("notification_log", tb => tb.HasComment("通知发送记录：通知中心每次对单渠道投递一条，记录最终状态与耗时"));

        entity.HasIndex(e => e.CreateTime, "idx_notification_log_time")
            .HasFilter("is_deleted = 0");

        entity.HasIndex(e => new { e.ChannelId, e.CreateTime }, "idx_notification_log_channel")
            .HasFilter("is_deleted = 0");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ChannelId)
            .HasComment("渠道 id")
            .HasColumnName("channel_id");
        entity.Property(e => e.ChannelName)
            .HasMaxLength(100)
            .HasComment("渠道名称快照（渠道删除后仍可读）")
            .HasColumnName("channel_name");
        entity.Property(e => e.ChannelType)
            .HasComment("渠道类型")
            .HasColumnName("channel_type");
        entity.Property(e => e.Content)
            .HasMaxLength(4000)
            .HasComment("通知正文（超长截断存档）")
            .HasColumnName("content");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DurationMs)
            .HasComment("发送耗时（毫秒）")
            .HasColumnName("duration_ms");
        entity.Property(e => e.Error)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("失败原因")
            .HasColumnName("error");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Level)
            .HasComment("通知级别")
            .HasColumnName("level");
        entity.Property(e => e.Status)
            .HasComment("发送状态")
            .HasColumnName("status");
        entity.Property(e => e.TargetUserIds)
            .HasMaxLength(1000)
            .HasComment("关联用户 id 快照（逗号分隔，定向个人推送时记录收件人；空为广播/全员）")
            .HasColumnName("target_user_ids");
        entity.Property(e => e.Recipients)
            .HasMaxLength(1000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("实际收件人快照（逗号分隔；SMTP 渠道记邮箱、短信渠道记手机号；固定地址渠道留空）")
            .HasColumnName("recipients");
        entity.Property(e => e.Title)
            .HasMaxLength(256)
            .HasComment("通知标题")
            .HasColumnName("title");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<NotificationLogEntity> modelBuilder);
}
