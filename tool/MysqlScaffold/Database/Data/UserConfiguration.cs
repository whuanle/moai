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
public partial class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("user", tb => tb.HasComment("用户"));

        entity.Property(e => e.Id).HasComment("用户ID");
        entity.Property(e => e.AvatarPath)
            .HasDefaultValueSql("''")
            .HasComment("头像路径");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Email).HasComment("邮箱");
        entity.Property(e => e.IsAdmin).HasComment("是否管理员");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.IsDisable).HasComment("禁用");
        entity.Property(e => e.NickName).HasComment("昵称");
        entity.Property(e => e.Password).HasComment("密码");
        entity.Property(e => e.PasswordSalt).HasComment("计算密码值的salt");
        entity.Property(e => e.Phone).HasComment("手机号");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");
        entity.Property(e => e.UserName).HasComment("用户名");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<UserEntity> modelBuilder);
}
