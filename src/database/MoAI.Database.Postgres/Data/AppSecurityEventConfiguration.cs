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

internal partial class AppSecurityEventConfiguration : IEntityTypeConfiguration<AppSecurityEventEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppSecurityEventEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_security_event_pkey");

        entity.ToTable("app_security_event");

        entity.HasIndex(e => e.AppId, "idx_app_security_event_app_id");

        entity.HasIndex(e => e.CreateTime, "idx_app_security_event_create_time");

        entity.HasIndex(e => e.TeamId, "idx_app_security_event_team_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasColumnName("id");
        entity.Property(e => e.Action).HasColumnName("action");
        entity.Property(e => e.AppId).HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.HitCount).HasColumnName("hit_count");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.RuleName).HasColumnName("rule_name");
        entity.Property(e => e.RuleType).HasColumnName("rule_type");
        entity.Property(e => e.Scope).HasColumnName("scope");
        entity.Property(e => e.SessionId).HasColumnName("session_id");
        entity.Property(e => e.Strategy).HasColumnName("strategy");
        entity.Property(e => e.TeamId).HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserId).HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppSecurityEventEntity> modelBuilder);
}
