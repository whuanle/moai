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
/// 团队成员.
/// </summary>
internal partial class TeamUserConfiguration : IEntityTypeConfiguration<TeamUserEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamUserEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_user_pkey");

        entity.ToTable("team_user", tb => tb.HasComment("团队成员"));

        entity.HasIndex(e => e.TeamId, "idx_team_user_team_id");

        entity.HasIndex(e => e.UserId, "idx_team_user_user_id");

        entity.HasIndex(e => new { e.TeamId, e.UserId }, "idx_team_user_team_user_live_uindex")
            .IsUnique()
            .HasFilter("is_deleted = false");

        entity.Property(e => e.Id)
            .HasComment("自增主键")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除：false=在团队中，true=已移出；审计钩子经接口适配写入")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Role)
            .HasComment("团队角色：0=Owner 1=Admin 2=Member")
            .HasColumnName("role");
        entity.Property(e => e.TeamId)
            .HasComment("团队ID")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserId)
            .HasComment("用户ID")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamUserEntity> modelBuilder);
}
