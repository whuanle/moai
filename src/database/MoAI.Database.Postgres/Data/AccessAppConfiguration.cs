using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Enums;
using MoAI.Database.Entities;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database;

/// <summary>
/// 应用接入.
/// </summary>
internal partial class AccessAppConfiguration : IEntityTypeConfiguration<AccessAppEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AccessAppEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_access_app_primary");

        entity.ToTable("access_app", tb => tb.HasComment("应用接入"));

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
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Key)
            .HasMaxLength(255)
            .HasComment("接入 appid 原文（moai-ac-/moai-sa- 前缀按层级区分）")
            .HasColumnName("key");
        entity.Property(e => e.KeyPrefix)
            .HasMaxLength(16)
            .HasComment("key 展示前缀")
            .HasColumnName("key_prefix");
        entity.Property(e => e.Level)
            .HasDefaultValue(AccessAppLevel.Team)
            .HasComment("接入层级：0=team 团队级 1=system 系统级")
            .HasColumnName("level");
        entity.Property(e => e.Secret)
            .HasMaxLength(255)
            .HasComment("接入密钥 PBKDF2 哈希（null=未设密钥）")
            .HasColumnName("secret");
        entity.Property(e => e.SecretSalt)
            .HasMaxLength(255)
            .HasComment("接入密钥盐（Base64）")
            .HasColumnName("secret_salt");
        entity.Property(e => e.Status)
            .HasDefaultValue(AccessAppStatus.Enabled)
            .HasComment("状态：0=enabled 1=disabled")
            .HasColumnName("status");
        entity.Property(e => e.LastUsedTime)
            .HasComment("最近使用时间（通过模型网关调用时刷新）")
            .HasColumnName("last_used_time");
        entity.Property(e => e.Name)
            .HasMaxLength(20)
            .HasComment("应用名称")
            .HasColumnName("name");
        entity.Property(e => e.Scopes)
            .HasDefaultValue(2038)
            .HasComment("功能范围位标记（位或组合）：团队级低位 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write 256=kg_mcp 512=app_acp 1024=app_a2a；系统级高位 65536=org_read 131072=org_write 262144=user_read 524288=user_write 1048576=team_read 2097152=team_write")
            .HasColumnName("scopes");
        entity.Property(e => e.TeamId)
            .HasComment("团队id（系统级接入固定 0）")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);

        entity.HasIndex(e => new { e.Key, e.IsDeleted }, "idx_access_app_key_uindex").IsUnique();
        entity.HasIndex(e => e.KeyPrefix, "idx_access_app_key_prefix");
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AccessAppEntity> modelBuilder);
}
