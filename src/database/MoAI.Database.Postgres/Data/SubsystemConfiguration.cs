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
/// 子系统.
/// </summary>
internal partial class SubsystemConfiguration : IEntityTypeConfiguration<SubsystemEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SubsystemEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_subsystem_primary");

        entity.ToTable("subsystem", tb => tb.HasComment("子系统：独立部署、通过 appid+密钥接入 MoAI 的外部子系统"));

        entity.HasIndex(e => e.Key)
            .IsUnique()
            .HasDatabaseName("idx_subsystem_key_uindex")
            .HasFilter("\"is_deleted\" = 0");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.Key)
            .HasMaxLength(40)
            .HasComment("子系统标识（appid），创建后不可修改")
            .HasColumnName("key");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("子系统名称")
            .HasColumnName("name");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::text")
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.Secret)
            .HasMaxLength(255)
            .HasComment("接入密钥哈希（PBKDF2），原文仅在创建/重置时返回一次")
            .HasColumnName("secret");
        entity.Property(e => e.SecretSalt)
            .HasMaxLength(255)
            .HasComment("接入密钥盐（Base64）")
            .HasColumnName("secret_salt");
        entity.Property(e => e.Status)
            .HasComment("状态：0=启用 1=停用，停用后拒绝换取 token 与机器面调用")
            .HasColumnName("status");
        entity.Property(e => e.TeamScope)
            .HasComment("团队授权范围：0=全员可用 1=指定团队")
            .HasColumnName("team_scope");
        entity.Property(e => e.CallbackUrl)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::text")
            .HasComment("委托 SSO 登录回调地址（子系统侧完整 URL），空串=未开放登录接入")
            .HasColumnName("callback_url");
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

    partial void OnConfigurePartial(EntityTypeBuilder<SubsystemEntity> modelBuilder);
}
