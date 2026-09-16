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
        entity.HasIndex(e => e.TeamId, "idx_app_workflow_instance_team_id");
        entity.HasIndex(e => e.WorkflowConfigId, "idx_app_workflow_instance_config_id");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("实例ID")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("所属应用ID，逻辑关联app.id")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.EndTime)
            .HasComment("结束时间（完成/挂起/取消）")
            .HasColumnName("end_time");
        entity.Property(e => e.ErrorMessage)
            .HasComment("失败/挂起原因，无异常为 null")
            .HasColumnName("error_message");
        entity.Property(e => e.Input)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("启动参数 JSON 对象文本，空为 '{}'")
            .HasColumnName("input");
        entity.Property(e => e.InstanceData)
            .HasComment("引擎实例全量 JSON（WorkflowInstance 序列化，含每个节点的状态/输入/输出/耗时），实例自含定义快照")
            .HasColumnName("instance_data");
        entity.Property(e => e.IsDebug)
            .HasComment("是否调试运行（设计器内发起），0=正式执行 1=调试")
            .HasColumnName("is_debug");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除（legacy bigint 约定）")
            .HasColumnName("is_deleted");
        entity.Property(e => e.Output)
            .HasComment("最终输出 JSON 对象文本，未产出为 null")
            .HasColumnName("output");
        entity.Property(e => e.StartTime)
            .HasComment("开始执行时间")
            .HasColumnName("start_time");
        entity.Property(e => e.Status)
            .HasComment("实例状态，0=已创建 1=执行中 2=已挂起 3=已完成 4=已取消")
            .HasColumnName("status");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.Version)
            .HasComment("执行引用的定义版本号，0=调试执行（运行草稿定义）")
            .HasColumnName("version");
        entity.Property(e => e.WorkflowConfigId)
            .HasComment("编排配置ID，逻辑关联app_workflow_config.id")
            .HasColumnName("workflow_config_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppWorkflowInstanceEntity> modelBuilder);
}
