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
            .HasComment("接入密钥存储值（系统级：v2: 前缀+AES 密文可回显，无前缀=存量 PBKDF2 哈希；团队级：PBKDF2 哈希；null=未设密钥）")
            .HasColumnName("secret");
        entity.Property(e => e.SecretMask)
            .HasMaxLength(32)
            .HasComment("密钥展示掩码（前 4 + **** + 后 4，仅系统级可逆格式行填充）")
            .HasColumnName("secret_mask");
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
        entity.Property(e => e.AppType)
            .HasDefaultValue(AccessAppType.None)
            .HasComment("系统级接入类型：0=不适用（团队级行）1=subsystem 子系统 2=app 应用接入")
            .HasColumnName("app_type");
        entity.Property(e => e.TeamScope)
            .HasDefaultValue(SubsystemTeamScope.AllTeams)
            .HasComment("团队授权范围（仅子系统行，委托 SSO 门禁用）：0=全员可用 1=指定团队")
            .HasColumnName("team_scope");
        entity.Property(e => e.CallbackUrls)
            .HasComment("委托 SSO 登录回调地址列表（JSON 字符串数组，仅子系统行使用，空数组=未开放登录接入）")
            .HasColumnName("callback_urls");
        entity.Property(e => e.Name)
            .HasMaxLength(50)
            .HasComment("应用名称")
            .HasColumnName("name");
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
        entity.Property(e => e.AvatarPath)
            .HasMaxLength(255)
            .HasComment("接入头像 objectKey，空串=未设置")
            .HasColumnName("avatar_path")
            .HasDefaultValue(string.Empty);

        OnConfigurePartial(entity);

        entity.HasIndex(e => new { e.Key, e.IsDeleted }, "idx_access_app_key_uindex").IsUnique();
        entity.HasIndex(e => e.KeyPrefix, "idx_access_app_key_prefix");
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AccessAppEntity> modelBuilder);
}
