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

internal partial class LoginIpBanConfiguration : IEntityTypeConfiguration<LoginIpBanEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LoginIpBanEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("login_ip_ban_pkey");

        entity.ToTable("login_ip_ban");

        entity.HasIndex(e => e.Ip, "idx_login_ip_ban_ip");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.ExpireTime)
            .HasComment("封禁到期时间")
            .HasColumnName("expire_time");
        entity.Property(e => e.Ip)
            .HasComment("封禁的 IP")
            .HasColumnName("ip");
        entity.Property(e => e.IsLifted).HasColumnName("is_lifted");
        entity.Property(e => e.LiftTime).HasColumnName("lift_time");
        entity.Property(e => e.LiftUserId).HasColumnName("lift_user_id");
        entity.Property(e => e.Reason).HasColumnName("reason");
        entity.Property(e => e.SourceRule).HasColumnName("source_rule");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<LoginIpBanEntity> modelBuilder);
}
