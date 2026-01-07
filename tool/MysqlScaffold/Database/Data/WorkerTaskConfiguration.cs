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
public partial class WorkerTaskConfiguration : IEntityTypeConfiguration<WorkerTaskEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkerTaskEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("worker_task", tb => tb.HasComment("工作任务"));

        entity.Property(e => e.Id)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("id");
        entity.Property(e => e.BindId).HasComment("关联对象id");
        entity.Property(e => e.BindType).HasComment("关联类型");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("创建时间");
        entity.Property(e => e.CreateUserId).HasComment("创建人");
        entity.Property(e => e.Data)
            .HasDefaultValueSql("'{}'")
            .HasComment("自定义数据,json格式");
        entity.Property(e => e.IsDeleted).HasComment("软删除");
        entity.Property(e => e.Message).HasComment("消息、错误信息");
        entity.Property(e => e.State).HasComment("任务状态，不同的任务类型状态值规则不一样");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("utc_timestamp()")
            .HasComment("最后更新时间");
        entity.Property(e => e.UpdateUserId).HasComment("最后修改人");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WorkerTaskEntity> modelBuilder);
}
