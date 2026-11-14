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
/// 应用收藏，(app_id, user_id) 唯一（活跃行），应用广场收藏置顶排序的数据源.
/// </summary>
internal partial class AppFavoriteConfiguration : IEntityTypeConfiguration<AppFavoriteEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppFavoriteEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_favorite_pkey");

        entity.ToTable("app_favorite", tb => tb.HasComment("应用收藏，(app_id, user_id) 唯一（活跃行），应用广场收藏置顶排序的数据源"));

        entity.HasIndex(e => new { e.AppId, e.UserId }, "idx_app_favorite_app_user_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("应用 id，逻辑关联 app.id")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserId)
            .HasComment("用户 id，逻辑关联 user.id")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppFavoriteEntity> modelBuilder);
}
