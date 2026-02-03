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
/// 工作任务.
/// </summary>
internal partial class WorkerTaskConfiguration : IEntityTypeConfiguration<WorkerTaskEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkerTaskEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("worker_task", tb => tb.HasComment("工作任务"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.BindId)
            .HasComment("关联对象id")
            .HasColumnType("int(11)")
            .HasColumnName("bind_id");
        entity.Property(e => e.BindType)
            .HasMaxLength(20)
            .HasComment("关联类型")
            .HasColumnName("bind_type");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Data)
            .HasDefaultValueSql("'{}'")
            .HasComment("自定义数据,json格式")
            .HasColumnType("json")
            .HasColumnName("data");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Message)
            .HasComment("消息、错误信息")
            .HasColumnType("text")
            .HasColumnName("message");
        entity.Property(e => e.State)
            .HasComment("任务状态，不同的任务类型状态值规则不一样")
            .HasColumnType("int(11)")
            .HasColumnName("state");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("最后修改人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WorkerTaskEntity> modelBuilder);
}
