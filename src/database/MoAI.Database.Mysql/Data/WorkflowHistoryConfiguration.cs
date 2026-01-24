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
/// 流程执行记录.
/// </summary>
public partial class WorkflowHistoryConfiguration : IEntityTypeConfiguration<WorkflowHistoryEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkflowHistoryEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("workflow_history", tb => tb.HasComment("流程执行记录"));

        entity.Property(e => e.Id)
            .HasMaxLength(16)
            .HasDefaultValueSql("unhex(replace(uuid(),'-',''))")
            .HasComment("varbinary(16)")
            .HasColumnName("id");
        entity.Property(e => e.Data)
            .HasDefaultValueSql("'{}'")
            .HasComment("数据内容")
            .HasColumnType("json")
            .HasColumnName("data");
        entity.Property(e => e.RunParamters)
            .HasComment("运行参数")
            .HasColumnType("json")
            .HasColumnName("run_paramters");
        entity.Property(e => e.State)
            .HasComment("工作状态")
            .HasColumnType("int(11)")
            .HasColumnName("state");
        entity.Property(e => e.SystemParamters)
            .HasComment("系统参数")
            .HasColumnType("json")
            .HasColumnName("system_paramters");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnType("int(11)")
            .HasColumnName("team_id");
        entity.Property(e => e.WorkflowDesignId)
            .HasMaxLength(16)
            .HasComment("流程设计id")
            .HasColumnName("workflow_design_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<WorkflowHistoryEntity> modelBuilder);
}
