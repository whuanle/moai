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
/// 团队成员，用户与团队多对多关联.
/// </summary>
internal partial class TeamUserConfiguration : IEntityTypeConfiguration<TeamUserEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamUserEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_user_pkey");

        entity.ToTable("team_user", tb => tb.HasComment("团队成员，用户与团队多对多关联"));

        entity.HasIndex(e => e.TeamId, "idx_team_user_team_id");

        entity.HasIndex(e => new { e.TeamId, e.UserId }, "idx_team_user_team_user_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.UserId, "idx_team_user_user_id");

        entity.Property(e => e.Id)
            .ValueGeneratedNever()
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("加入时间，审计钩子自动填充，默认timezone(utc,now())")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("邀请人用户ID，审计钩子插入时自动填充")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Role)
            .HasDefaultValue(2)
            .HasComment("成员角色：0=Owner(所有者，可解散/转让/管理一切) 1=Admin(可管理成员、创建团队资源) 2=Member(普通成员)，新成员默认2")
            .HasColumnName("role");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID，逻辑关联team.id（仓库约定不建物理外键）")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间，审计钩子插入/更新/删除时自动刷新")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人用户ID，角色变更/移除时审计钩子自动填充")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserId)
            .HasComment("成员用户ID，逻辑关联user.id（仓库约定不建物理外键）")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamUserEntity> modelBuilder);
}
