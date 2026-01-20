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
public partial class AppConfiguration : IEntityTypeConfiguration<AppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("app", tb => tb.HasComment("应用"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppType)
            .HasComment("应用类型，普通应用=0,流程编排=1")
            .HasColumnType("int(11)")
            .HasColumnName("app_type");
        entity.Property(e => e.Avatar)
            .HasMaxLength(255)
            .HasDefaultValueSql("''")
            .HasComment("头像 objectKey")
            .HasColumnName("avatar");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.ClassifyId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("classify_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsAuth)
    .HasComment("是否开启授权验证")
    .HasColumnName("is_auth");
        entity.Property(e => e.IsDisable)
            .HasComment("禁用")
            .HasColumnName("is_disable");
        entity.Property(e => e.IsForeign)
            .HasComment("是否外部应用")
            .HasColumnName("is_foreign");
        entity.Property(e => e.IsPublic)
            .HasComment("公开到团队外使用")
            .HasColumnName("is_public");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("应用名称")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppEntity> modelBuilder);
}
