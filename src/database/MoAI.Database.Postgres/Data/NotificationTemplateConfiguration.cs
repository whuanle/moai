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
/// 通知中心模板.
/// </summary>
internal partial class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplateEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<NotificationTemplateEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_notification_template_primary");

        entity.ToTable("notification_template", tb => tb.HasComment("通知中心模板：文本 + 变量定义，发送时插值生成通知内容；可携带阿里云/腾讯云短信平台已审核模板映射"));

        entity.HasIndex(e => e.Name, "idx_notification_template_name_uindex")
            .IsUnique()
            .HasFilter("is_deleted = 0");

        entity.HasIndex(e => e.Code, "idx_notification_template_code_uindex")
            .IsUnique()
            .HasFilter("is_deleted = 0 AND code <> ''");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Code)
            .HasMaxLength(64)
            .HasDefaultValueSql("''::character varying")
            .HasComment("内置模板标识 code（非空表示平台内置模板：不可删除、可编辑，业务按 code 回退解析）")
            .HasColumnName("code");
        entity.Property(e => e.AliyunTemplateCode)
            .HasMaxLength(128)
            .HasDefaultValueSql("''::character varying")
            .HasComment("阿里云短信模板 Code（平台审核通过后回填，空表示不用于阿里云短信）")
            .HasColumnName("aliyun_template_code");
        entity.Property(e => e.Content)
            .HasMaxLength(10000)
            .HasComment("通知正文模板（纯文本或 HTML，含 {{变量}} 双花括号占位符）")
            .HasColumnName("content");
        entity.Property(e => e.ContentType)
            .HasDefaultValueSql("0")
            .HasComment("内容类型")
            .HasColumnName("content_type");
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
            .HasComment("模板描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Name)
            .HasMaxLength(100)
            .HasComment("模板名称（唯一）")
            .HasColumnName("name");
        entity.Property(e => e.Title)
            .HasMaxLength(256)
            .HasDefaultValueSql("''::character varying")
            .HasComment("通知主题模板（邮件主题用，短信无主题）")
            .HasColumnName("title");
        entity.Property(e => e.TencentTemplateId)
            .HasMaxLength(128)
            .HasDefaultValueSql("''::character varying")
            .HasComment("腾讯云短信模板 Id（平台审核通过后回填，空表示不用于腾讯云短信）")
            .HasColumnName("tencent_template_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.Variables)
            .HasMaxLength(4000)
            .HasDefaultValueSql("''::character varying")
            .HasComment("变量定义 JSON（[{\"name\",\"description\",\"example\"}]，发送时按 name 提供变量值）")
            .HasColumnName("variables");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<NotificationTemplateEntity> modelBuilder);
}
