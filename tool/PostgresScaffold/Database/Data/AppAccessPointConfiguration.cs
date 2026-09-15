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
/// 外部应用访问点配置，与外部应用 1:1.
/// </summary>
internal partial class AppAccessPointConfiguration : IEntityTypeConfiguration<AppAccessPointEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppAccessPointEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_access_point_pkey");

        entity.ToTable("app_access_point", tb => tb.HasComment("外部应用访问点配置，与外部应用 1:1"));

        entity.HasIndex(e => e.AppId, "idx_app_access_point_app_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("外部应用id，1:1（partial 唯一）")
            .HasColumnName("app_id");
        entity.Property(e => e.Avatar)
            .HasMaxLength(255)
            .HasComment("头像 objectKey（走存储）")
            .HasColumnName("avatar");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
        entity.Property(e => e.DefaultOpen)
            .HasComment("是否默认展开")
            .HasColumnName("default_open");
        entity.Property(e => e.Enabled)
            .HasDefaultValue(true)
            .HasComment("是否启用访问点")
            .HasColumnName("enabled");
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
        entity.Property(e => e.LauncherText)
            .HasMaxLength(50)
            .HasComment("悬浮按钮文案，空则用图标")
            .HasColumnName("launcher_text");
        entity.Property(e => e.PanelHeight)
            .HasDefaultValue(560)
            .HasComment("面板高度 px")
            .HasColumnName("panel_height");
        entity.Property(e => e.PanelWidth)
            .HasDefaultValue(380)
            .HasComment("面板宽度 px")
            .HasColumnName("panel_width");
        entity.Property(e => e.Placeholder)
            .HasMaxLength(100)
            .HasComment("输入框占位文案")
            .HasColumnName("placeholder");
        entity.Property(e => e.Position)
            .HasMaxLength(20)
            .HasDefaultValueSql("'bottomRight'::character varying")
            .HasComment("悬浮位置：bottom-right / bottom-left")
            .HasColumnName("position");
        entity.Property(e => e.PrimaryColor)
            .HasMaxLength(20)
            .HasComment("主题色，#RRGGBB")
            .HasColumnName("primary_color");
        entity.Property(e => e.Subtitle)
            .HasMaxLength(255)
            .HasComment("欢迎语/副标题")
            .HasColumnName("subtitle");
        entity.Property(e => e.TeamId)
            .HasComment("归属团队id，冗余团队维度过滤")
            .HasColumnName("team_id");
        entity.Property(e => e.Title)
            .HasMaxLength(100)
            .HasComment("面板标题，空则用应用名")
            .HasColumnName("title");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId).HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppAccessPointEntity> modelBuilder);
}
