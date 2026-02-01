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
/// 用户.
/// </summary>
internal partial class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("user_pkey");

        entity.ToTable("user", tb => tb.HasComment("用户"));

        entity.HasIndex(e => e.Email, "idx_users_email");

        entity.HasIndex(e => e.Phone, "idx_users_phone");

        entity.HasIndex(e => e.UserName, "idx_users_user_name");

        entity.HasIndex(e => new { e.Email, e.IsDeleted }, "users_email_is_deleted_uindex").IsUnique();

        entity.HasIndex(e => new { e.Phone, e.IsDeleted }, "users_phone_is_deleted_uindex").IsUnique();

        entity.HasIndex(e => new { e.UserName, e.IsDeleted }, "users_user_name_is_deleted_uindex").IsUnique();

        entity.Property(e => e.Id)
            .HasComment("用户ID")
            .HasColumnName("id");
        entity.Property(e => e.AvatarPath)
            .HasMaxLength(255)
            .HasComment("头像路径")
            .HasColumnName("avatar_path");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("now()")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Email)
            .HasMaxLength(255)
            .HasComment("邮箱")
            .HasColumnName("email");
        entity.Property(e => e.IsAdmin)
            .HasComment("是否管理员")
            .HasColumnName("is_admin");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(0L)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsDisable)
            .HasComment("禁用")
            .HasColumnName("is_disable");
        entity.Property(e => e.NickName)
            .HasMaxLength(50)
            .HasComment("昵称")
            .HasColumnName("nick_name");
        entity.Property(e => e.Password)
            .HasMaxLength(255)
            .HasComment("密码")
            .HasColumnName("password");
        entity.Property(e => e.PasswordSalt)
            .HasMaxLength(255)
            .HasComment("计算密码值的salt")
            .HasColumnName("password_salt");
        entity.Property(e => e.Phone)
            .HasMaxLength(20)
            .HasComment("手机号")
            .HasColumnName("phone");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("now()")
            .HasComment("最后更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserName)
            .HasMaxLength(50)
            .HasComment("用户名")
            .HasColumnName("user_name");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<UserEntity> modelBuilder);
}
