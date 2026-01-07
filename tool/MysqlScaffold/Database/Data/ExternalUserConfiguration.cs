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
/// 外部系统的用户.
/// </summary>
public partial class ExternalUserConfiguration : IEntityTypeConfiguration<ExternalUserEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalUserEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("external_user", tb => tb.HasComment("外部系统的用户"));

        entity.Property(e => e.Id).HasComment("用户ID");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.ExternalAppId).HasComment("所属的外部应用id");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");
        entity.Property(e => e.UserUid).HasComment("外部用户标识");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ExternalUserEntity> modelBuilder);
}
