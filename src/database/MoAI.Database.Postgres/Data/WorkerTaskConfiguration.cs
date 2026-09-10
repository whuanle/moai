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
/// 通用任务.
/// </summary>
internal partial class WorkerTaskConfiguration : IEntityTypeConfiguration<WorkerTaskEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkerTaskEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_worker_task_primary");

        entity.ToTable("worker_task", tb => tb.HasComment("通用任务"));

        entity.HasIndex(e => new { e.BindType, e.BindId, e.State }, "idx_worker_task_binding_state");
        entity
            .HasIndex(e => new { e.BindType, e.BindId }, "ux_worker_task_bind_active")
            .IsUnique()
            .HasFilter("is_deleted = 0 AND state IN (1, 2)");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.BindType)
            .HasMaxLength(20)
            .HasComment("绑定类型")
            .HasColumnName("bind_type");
        entity.Property(e => e.BindId)
            .HasComment("绑定id")
            .HasColumnName("bind_id");
        entity.Property(e => e.State)
            .HasComment("状态")
            .HasColumnName("state");
        entity.Property(e => e.Message)
            .HasComment("消息")
            .HasColumnName("message");
        entity.Property(e => e.Data)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("扩展数据")
            .HasColumnName("data");
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
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WorkerTaskEntity> modelBuilder);
}