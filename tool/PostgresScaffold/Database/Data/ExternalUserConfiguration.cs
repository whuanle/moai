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
/// 外部用户（外部应用接入的身份记录）.
/// </summary>
internal partial class ExternalUserConfiguration : IEntityTypeConfiguration<ExternalUserEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalUserEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("external_user_pkey");

        entity.ToTable("external_user", tb => tb.HasComment("外部用户（外部应用接入的身份记录）"));

        entity.HasIndex(e => new { e.AccessAppId, e.ExternalUserId }, "idx_external_user_accessapp_user_uindex")
            .IsUnique()
            .HasFilter("((access_app_id IS NOT NULL) AND (is_deleted = 0))");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("nextval('external_id_seq'::regclass)")
            .HasComment("外部用户id，自增主键，承载会话 create_user_id 与用量 user_id")
            .HasColumnName("id");
        entity.Property(e => e.AccessAppId)
            .HasComment("来源应用接入id（access_app.key 换取 token 时写入），匿名访问为 null")
            .HasColumnName("access_app_id");
        entity.Property(e => e.AppId)
            .HasComment("授权访问的应用id，is_auth=false 匿名访问时为来源应用，用户 token 指定授权的单个应用")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
        entity.Property(e => e.ExternalUserId)
            .HasMaxLength(128)
            .HasComment("外部身份标识，第三方系统的用户唯一标识或临时随机值")
            .HasColumnName("external_user_id");
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
        entity.Property(e => e.Nickname)
            .HasMaxLength(100)
            .HasComment("外部用户显示名，可选")
            .HasColumnName("nickname");
        entity.Property(e => e.TeamId)
            .HasComment("归属团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId).HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ExternalUserEntity> modelBuilder);
}
