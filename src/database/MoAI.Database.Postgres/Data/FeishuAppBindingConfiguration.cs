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
/// 飞书应用绑定，将飞书应用绑定到应用等渠道；同一飞书应用同时只能绑定一个渠道.
/// </summary>
internal partial class FeishuAppBindingConfiguration : IEntityTypeConfiguration<FeishuAppBindingEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FeishuAppBindingEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("feishu_app_binding_pkey");

        entity.ToTable("feishu_app_binding", tb => tb.HasComment("飞书应用绑定，将飞书应用绑定到应用等渠道；同一飞书应用同时只能绑定一个渠道"));

        entity.HasIndex(e => new { e.ChannelType, e.ChannelId }, "idx_feishu_app_binding_channel_index");

        entity.HasIndex(e => e.FeishuAppId, "idx_feishu_app_binding_feishu_app_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.ChannelId)
            .HasMaxLength(64)
            .HasComment("渠道记录 id 字符串，应用渠道为 app.id（uuid）")
            .HasColumnName("channel_id");
        entity.Property(e => e.ChannelType)
            .HasComment("渠道类型，见 FeishuChannelType")
            .HasColumnName("channel_type");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FeishuAppId)
            .HasComment("飞书应用记录 id（feishu_app.id，逻辑关联，仓库约定不建物理外键）")
            .HasColumnName("feishu_app_id");
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

    partial void OnConfigurePartial(EntityTypeBuilder<FeishuAppBindingEntity> modelBuilder);
}
