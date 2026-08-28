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
/// 应用.
/// </summary>
internal partial class AppConfiguration : IEntityTypeConfiguration<AppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65606_primary");

        entity.ToTable("app", tb => tb.HasComment("应用"));

        entity.HasIndex(e => e.Name, "idx_65606_app_name_index");

        entity.HasIndex(e => e.TeamId, "idx_65606_app_team_id_index");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppType)
            .HasDefaultValue(0)
            .HasComment("应用类型，普通应用=0,流程编排=1")
            .HasColumnName("app_type");
        entity.Property(e => e.Avatar)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("头像 objectKey")
            .HasColumnName("avatar");
        entity.Property(e => e.ClassifyId)
            .HasDefaultValue(0)
            .HasComment("分类id")
            .HasColumnName("classify_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsAuth)
            .HasDefaultValue(false)
            .HasComment("是否开启授权才能使用，只有外部应用可以设置")
            .HasColumnName("is_auth");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasDefaultValue(false)
            .HasComment("禁用")
            .HasColumnName("is_disable");
        entity.Property(e => e.IsForeign)
            .HasDefaultValue(false)
            .HasComment("是否外部应用")
            .HasColumnName("is_foreign");
        entity.Property(e => e.IsPublic)
            .HasDefaultValue(false)
            .HasComment("公开到团队外使用")
            .HasColumnName("is_public");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("应用名称")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppEntity> modelBuilder);
}
