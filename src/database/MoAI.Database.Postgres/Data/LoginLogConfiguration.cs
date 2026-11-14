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

internal partial class LoginLogConfiguration : IEntityTypeConfiguration<LoginLogEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LoginLogEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("login_log_pkey");

        entity.ToTable("login_log");

        entity.HasIndex(e => e.CreateTime, "idx_login_log_create_time");

        entity.HasIndex(e => e.Ip, "idx_login_log_ip");

        entity.HasIndex(e => e.UserId, "idx_login_log_user_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasColumnName("id");
        entity.Property(e => e.City).HasColumnName("city");
        entity.Property(e => e.Country).HasColumnName("country");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.FailReason).HasColumnName("fail_reason");
        entity.Property(e => e.Ip).HasColumnName("ip");
        entity.Property(e => e.Isp).HasColumnName("isp");
        entity.Property(e => e.IsSuccess).HasColumnName("is_success");
        entity.Property(e => e.LoginType).HasColumnName("login_type");
        entity.Property(e => e.Province).HasColumnName("province");
        entity.Property(e => e.RiskLevel).HasColumnName("risk_level");
        entity.Property(e => e.RiskReasons).HasColumnName("risk_reasons");
        entity.Property(e => e.UserAgent).HasColumnName("user_agent");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.UserName).HasColumnName("user_name");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<LoginLogEntity> modelBuilder);
}
