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
/// 团队.
/// </summary>
internal partial class TeamConfiguration : IEntityTypeConfiguration<TeamEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_pkey");

        entity.ToTable("team", tb => tb.HasComment("团队"));

        entity.HasIndex(e => e.Name, "idx_team_name_live_uindex")
            .IsUnique()
            .HasFilter("is_deleted = false");

        entity.Property(e => e.Id)
            .HasComment("团队ID")
            .HasColumnName("id");
        entity.Property(e => e.AvatarPath)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("团队头像路径，存储文件 ObjectKey")
            .HasColumnName("avatar_path");
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
            .HasComment("团队简介")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除：false=未删除，true=已删除；审计钩子经接口适配写入")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasComment("禁用")
            .HasColumnName("is_disable");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("团队名称")
            .HasColumnName("name");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamEntity> modelBuilder);
}
