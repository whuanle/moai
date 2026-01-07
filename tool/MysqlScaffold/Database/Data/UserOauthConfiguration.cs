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
/// oauth2.0对接.
/// </summary>
public partial class UserOauthConfiguration : IEntityTypeConfiguration<UserOauthEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserOauthEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("user_oauth", tb => tb.HasComment("oauth2.0对接"));

        entity.Property(e => e.Id).HasComment("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.ProviderId).HasComment("供应商id,对应oauth_connection表");
        entity.Property(e => e.Sub).HasComment("用户oauth对应的唯一id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");
        entity.Property(e => e.UserId).HasComment("用户id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<UserOauthEntity> modelBuilder);
}
