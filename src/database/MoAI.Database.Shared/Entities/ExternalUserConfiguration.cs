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
internal partial class ExternalUserConfiguration : IEntityTypeConfiguration<ExternalUserEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalUserEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65753_primary");

        entity.ToTable("external_user", tb => tb.HasComment("外部系统的用户"));

        entity.Property(e => e.Id)
            .HasComment("用户ID")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.ExternalAppId)
            .HasComment("所属的外部应用id")
            .HasColumnName("external_app_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserUid)
            .HasMaxLength(50)
            .HasComment("外部用户标识")
            .HasColumnName("user_uid");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<ExternalUserEntity> modelBuilder);
}
