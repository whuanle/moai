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
/// 钉钉应用绑定，将钉钉应用绑定到团队应用等渠道；应用渠道独占.
/// </summary>
internal partial class DingTalkAppBindingConfiguration : IEntityTypeConfiguration<DingTalkAppBindingEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DingTalkAppBindingEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("dingtalk_app_binding_pkey");

        entity.ToTable("dingtalk_app_binding", tb => tb.HasComment("钉钉应用绑定，将钉钉应用绑定到团队应用等渠道；应用渠道独占"));

        // 应用渠道独占：同一钉钉应用只能绑定一个团队应用
        entity.HasIndex(e => e.DingTalkAppId, "idx_dingtalk_app_binding_app_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0 and channel_type = 0)");

        // 同一钉钉应用对同一渠道记录只允许一条绑定
        entity.HasIndex(e => new { e.DingTalkAppId, e.ChannelType, e.ChannelId }, "idx_dingtalk_app_binding_channel_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => new { e.ChannelType, e.ChannelId }, "idx_dingtalk_app_binding_channel_index");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.ChannelId)
            .HasMaxLength(64)
            .HasComment("渠道记录 id 字符串，应用渠道为 app.id（uuid）")
            .HasColumnName("channel_id");
        entity.Property(e => e.ChannelType)
            .HasComment("渠道类型，见 DingTalkChannelType（当前仅 0=app 独占型）")
            .HasColumnName("channel_type");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DingTalkAppId)
            .HasComment("钉钉应用记录 id（dingtalk_app.id，逻辑关联，仓库约定不建物理外键）")
            .HasColumnName("dingtalk_app_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<DingTalkAppBindingEntity> modelBuilder);
}
