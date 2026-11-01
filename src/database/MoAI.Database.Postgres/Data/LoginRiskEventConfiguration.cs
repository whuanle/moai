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

internal partial class LoginRiskEventConfiguration : IEntityTypeConfiguration<LoginRiskEventEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LoginRiskEventEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("login_risk_event_pkey");

        entity.ToTable("login_risk_event");

        entity.HasIndex(e => e.CreateTime, "idx_login_risk_event_create_time");

        entity.HasIndex(e => e.IsRead, "idx_login_risk_event_is_read");

        entity.HasIndex(e => e.UserId, "idx_login_risk_event_user_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasColumnName("id");
        entity.Property(e => e.City).HasColumnName("city");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Detail).HasColumnName("detail");
        entity.Property(e => e.Ip).HasColumnName("ip");
        entity.Property(e => e.IsRead).HasColumnName("is_read");
        entity.Property(e => e.Province).HasColumnName("province");
        entity.Property(e => e.RiskLevel).HasColumnName("risk_level");
        entity.Property(e => e.RuleCode).HasColumnName("rule_code");
        entity.Property(e => e.Summary).HasColumnName("summary");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.UserName).HasColumnName("user_name");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<LoginRiskEventEntity> modelBuilder);
}
