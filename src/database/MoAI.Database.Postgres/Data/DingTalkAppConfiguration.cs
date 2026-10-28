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
/// 钉钉应用，一个钉钉开放平台企业内部应用对应一条 Stream 长连接，机器人消息统一接收后按绑定转发.
/// </summary>
internal partial class DingTalkAppConfiguration : IEntityTypeConfiguration<DingTalkAppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DingTalkAppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("dingtalk_app_pkey");

        entity.ToTable("dingtalk_app", tb => tb.HasComment("钉钉应用，一个钉钉开放平台企业内部应用对应一条 Stream 长连接，机器人消息统一接收后按绑定转发"));

        entity.HasIndex(e => e.AppKey, "idx_dingtalk_app_app_key_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_dingtalk_app_team_id_index");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppKey)
            .HasMaxLength(64)
            .HasComment("钉钉应用 AppKey（新版开发者后台为 Client ID），全局唯一")
            .HasColumnName("app_key");
        entity.Property(e => e.AppSecret)
            .HasMaxLength(128)
            .HasComment("钉钉应用 AppSecret（新版开发者后台为 Client Secret）")
            .HasColumnName("app_secret");
        entity.Property(e => e.ChatbotUserId)
            .HasMaxLength(128)
            .HasDefaultValueSql("''::character varying")
            .HasComment("机器人的 dingtalk id（回调报文 chatbotUserId，加密串），首次收到机器人消息时回填")
            .HasColumnName("chatbot_user_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasComment("禁用，禁用后断开长连接且不再接收事件")
            .HasColumnName("is_disable");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("连接名称，团队内唯一")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<DingTalkAppEntity> modelBuilder);
}
