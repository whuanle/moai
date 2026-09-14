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
/// 飞书应用，一个飞书开放平台应用对应一条长连接，事件统一接收后按绑定转发.
/// </summary>
internal partial class FeishuAppConfiguration : IEntityTypeConfiguration<FeishuAppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FeishuAppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("feishu_app_pkey");

        entity.ToTable("feishu_app", tb => tb.HasComment("飞书应用，一个飞书开放平台应用对应一条长连接，事件统一接收后按绑定转发"));

        entity.HasIndex(e => e.AppId, "idx_feishu_app_app_id_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_feishu_app_team_id_index");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasMaxLength(64)
            .HasComment("飞书开放平台 AppID，形如 cli_xxx，全局唯一")
            .HasColumnName("app_id");
        entity.Property(e => e.AppSecret)
            .HasMaxLength(128)
            .HasComment("飞书开放平台 AppSecret")
            .HasColumnName("app_secret");
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
        entity.Property(e => e.Domain)
            .HasMaxLength(100)
            .HasDefaultValueSql("'https://open.feishu.cn'::character varying")
            .HasComment("接入域名，飞书为 https://open.feishu.cn，Lark 为 https://open.larksuite.com")
            .HasColumnName("domain");
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

    partial void OnConfigurePartial(EntityTypeBuilder<FeishuAppEntity> modelBuilder);
}
