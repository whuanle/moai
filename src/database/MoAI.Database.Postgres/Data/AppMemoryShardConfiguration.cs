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
/// 应用记忆分片登记：用户在某应用下的长期记忆行数，支撑跨应用发现与清空.
/// </summary>
internal partial class AppMemoryShardConfiguration : IEntityTypeConfiguration<AppMemoryShardEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppMemoryShardEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_memory_shard_pkey");

        entity.ToTable("app_memory_shard", tb => tb.HasComment("应用记忆分片登记：用户在某应用下的长期记忆行数，支撑跨应用发现与清空"));

        entity.HasIndex(e => e.AppId, "idx_app_memory_shard_app_id");

        entity.HasIndex(e => e.UserId, "idx_app_memory_shard_user_id");

        entity.HasIndex(e => new { e.AppId, e.UserId, e.IsDeleted }, "ux_app_memory_shard_app_user_live_uindex")
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
        entity.Property(e => e.LastActiveTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("最近一次记忆写入时间")
            .HasColumnName("last_active_time");
        entity.Property(e => e.MemoryCount)
            .HasComment("该用户在该应用向量集合中的记忆条数")
            .HasColumnName("memory_count");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队 id，逻辑关联 app.team_id")
            .HasColumnName("team_id");
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

    partial void OnConfigurePartial(EntityTypeBuilder<AppMemoryShardEntity> modelBuilder);
}
