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
/// 站内信.
/// </summary>
internal partial class MessageConfiguration : IEntityTypeConfiguration<MessageEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_message_primary");

        entity.ToTable("message", tb => tb.HasComment("站内信：每收件人一行，已读态/软删独立"));

        entity.HasIndex(e => new { e.RecipientUserId, e.IsRead }, "idx_message_recipient_read")
            .HasFilter("is_deleted = 0");

        entity.HasIndex(e => new { e.RecipientUserId, e.CreateTime }, "idx_message_recipient_time")
            .HasFilter("is_deleted = 0");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Content)
            .HasMaxLength(4000)
            .HasComment("正文")
            .HasColumnName("content");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsRead)
            .HasComment("是否已读")
            .HasColumnName("is_read");
        entity.Property(e => e.MessageType)
            .HasComment("消息类型")
            .HasColumnName("message_type");
        entity.Property(e => e.Params)
            .HasMaxLength(1024)
            .HasComment("扩展参数（可选 JSON 文本，供前端跳转定位）")
            .HasColumnName("params");
        entity.Property(e => e.ReadTime)
            .HasComment("已读时间")
            .HasColumnName("read_time");
        entity.Property(e => e.RecipientUserId)
            .HasComment("接收人用户 id")
            .HasColumnName("recipient_user_id");
        entity.Property(e => e.SenderUserId)
            .HasComment("发送人用户 id，空为系统发送")
            .HasColumnName("sender_user_id");
        entity.Property(e => e.Source)
            .HasMaxLength(50)
            .HasDefaultValueSql("''::character varying")
            .HasComment("来源模块标识，如 publication / team，空为平台")
            .HasColumnName("source");
        entity.Property(e => e.Title)
            .HasMaxLength(256)
            .HasComment("标题")
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

    partial void OnConfigurePartial(EntityTypeBuilder<MessageEntity> modelBuilder);
}
