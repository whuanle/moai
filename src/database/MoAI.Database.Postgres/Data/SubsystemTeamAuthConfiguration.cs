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
/// 子系统团队授权.
/// </summary>
internal partial class SubsystemTeamAuthConfiguration : IEntityTypeConfiguration<SubsystemTeamAuthEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SubsystemTeamAuthEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_subsystem_team_auth_primary");

        entity.ToTable("subsystem_team_auth", tb => tb.HasComment("子系统团队授权：指定团队范围下记录哪些团队可使用该子系统"));

        entity.HasIndex(e => new { e.SubsystemId, e.TeamId })
            .IsUnique()
            .HasDatabaseName("idx_subsystem_team_auth_uindex")
            .HasFilter("\"is_deleted\" = 0");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.SubsystemId)
            .HasComment("子系统id")
            .HasColumnName("subsystem_id");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
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

    partial void OnConfigurePartial(EntityTypeBuilder<SubsystemTeamAuthEntity> modelBuilder);
}
