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
/// 团队，知识库/插件等资源的管理单元.
/// </summary>
internal partial class TeamConfiguration : IEntityTypeConfiguration<TeamEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_pkey");

        entity.ToTable("team", tb => tb.HasComment("团队，知识库/插件等资源的管理单元"));

        entity.HasIndex(e => e.Name, "idx_team_name_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = false)");

        entity.Property(e => e.Id)
            .HasComment("团队ID，自增主键")
            .HasColumnName("id");
        entity.Property(e => e.AvatarPath)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("团队头像的存储ObjectKey（桶内路径），非完整URL，展示时经GetPublicFileUrl转公开地址，空串=未设置")
            .HasColumnName("avatar_path");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间，审计钩子自动填充，默认timezone(utc,now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人用户ID，审计钩子插入时自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("团队简介，最长255字符，空串=未填写")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除：false=未删除，true=已删除（审计钩子经接口适配自动写入）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasComment("是否禁用团队：true=团队及下级资源停用，由管理员操作，不影响成员账号登录")
            .HasColumnName("is_disable");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("团队名称，最长50字符")
            .HasColumnName("name");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间，审计钩子插入/更新/删除时自动刷新")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人用户ID，审计钩子更新/删除时自动填充")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamEntity> modelBuilder);
}
