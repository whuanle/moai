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
/// 团队模型网关API密钥，团队管理员创建并维护，团队成员使用密钥通过 /v1 开放接口调用团队已授权的模型.
/// </summary>
internal partial class TeamApiKeyConfiguration : IEntityTypeConfiguration<TeamApiKeyEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamApiKeyEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_team_api_key_primary");

        entity.ToTable("team_api_key", tb => tb.HasComment("团队模型网关API密钥，团队管理员创建并维护，团队成员使用密钥通过 /v1 开放接口调用团队已授权的模型"));

        entity.HasIndex(e => e.KeySha256, "idx_team_api_key_sha256_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_team_api_key_team_id_index");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.CreatorUserId)
            .HasComment("创建密钥的管理员用户id，密钥的可用性与该用户状态绑定")
            .HasColumnName("creator_user_id");
        entity.Property(e => e.ExpireTime)
            .HasComment("过期时间，null=永不过期")
            .HasColumnName("expire_time");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasComment("是否禁用")
            .HasColumnName("is_disable");
        entity.Property(e => e.KeyPrefix)
            .HasMaxLength(32)
            .HasComment("密钥前缀，仅用于列表展示，如 moai-Ab12CdEf")
            .HasColumnName("key_prefix");
        entity.Property(e => e.KeySha256)
            .HasComment("密钥的sha256，密钥原文不落库")
            .HasColumnName("key_sha256");
        entity.Property(e => e.LastUsedTime)
            .HasComment("最近一次调用时间，null=创建后从未使用")
            .HasColumnName("last_used_time");
        entity.Property(e => e.Name)
            .HasMaxLength(100)
            .HasComment("密钥名称")
            .HasColumnName("name");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamApiKeyEntity> modelBuilder);
}
