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
/// 流程应用运行实例，一次工作流执行的完整快照（含节点级状态，支撑断点恢复）.
/// </summary>
internal partial class AppWorkflowInstanceConfiguration : IEntityTypeConfiguration<AppWorkflowInstanceEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppWorkflowInstanceEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("app_workflow_instance_pkey");

        entity.ToTable("app_workflow_instance", tb => tb.HasComment("流程应用运行实例，一次工作流执行的完整快照（含节点级状态，支撑断点恢复）"));

        entity.HasIndex(e => e.AppId, "idx_app_workflow_instance_app_id");

        entity.HasIndex(e => e.WorkflowConfigId, "idx_app_workflow_instance_config_id");

        entity.HasIndex(e => e.TeamId, "idx_app_workflow_instance_team_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasColumnName("id");
        entity.Property(e => e.AppId).HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
        entity.Property(e => e.EndTime).HasColumnName("end_time");
        entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
        entity.Property(e => e.Input)
            .HasDefaultValueSql("'{}'::text")
            .HasColumnName("input");
        entity.Property(e => e.InstanceData)
            .HasComment("引擎实例全量 JSON（WorkflowInstance 序列化，含每个节点的状态/输入/输出/耗时），实例自含定义快照")
            .HasColumnName("instance_data");
        entity.Property(e => e.IsDebug)
            .HasComment("是否调试运行（设计器内发起），0=正式执行 1=调试")
            .HasColumnName("is_debug");
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
        entity.Property(e => e.Output).HasColumnName("output");
        entity.Property(e => e.StartTime).HasColumnName("start_time");
        entity.Property(e => e.Status)
            .HasComment("实例状态，0=已创建 1=执行中 2=已挂起 3=已完成 4=已取消")
            .HasColumnName("status");
        entity.Property(e => e.TeamId).HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId).HasColumnName("update_user_id");
        entity.Property(e => e.Version)
            .HasComment("执行引用的定义版本号，0=调试执行（运行草稿定义）")
            .HasColumnName("version");
        entity.Property(e => e.WorkflowConfigId).HasColumnName("workflow_config_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppWorkflowInstanceEntity> modelBuilder);
}
