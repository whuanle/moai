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
internal partial class UserOauthConfiguration : IEntityTypeConfiguration<UserOauthEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserOauthEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_63330_primary");

        entity.ToTable("user_oauth", tb => tb.HasComment("oauth2.0对接"));

        entity.HasIndex(e => new { e.ProviderId, e.Sub, e.IsDeleted }, "idx_63330_user_oauth_provider_id_sub_is_deleted_uindex").IsUnique();

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ProviderId)
            .HasComment("供应商id,对应oauth_connection表")
            .HasColumnName("provider_id");
        entity.Property(e => e.Sub)
            .HasMaxLength(50)
            .HasComment("用户oauth对应的唯一id")
            .HasColumnName("sub");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserId)
            .HasComment("用户id")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<UserOauthEntity> modelBuilder);
}
