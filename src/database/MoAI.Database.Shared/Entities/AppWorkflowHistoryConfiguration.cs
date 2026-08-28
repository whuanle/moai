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
internal partial class AppWorkflowHistoryConfiguration : IEntityTypeConfiguration<AppWorkflowHistoryEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppWorkflowHistoryEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65716_primary");

        entity.ToTable("app_workflow_history", tb => tb.HasComment("流程执行记录"));

        entity.Property(e => e.Id)
            .HasComment("varbinary(16)")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("应用id")
            .HasColumnName("app_id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Data)
            .HasDefaultValueSql("'{}'::text")
            .HasComment("数据内容")
            .HasColumnName("data");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.RunParamters)
            .HasComment("运行参数")
            .HasColumnName("run_paramters");
        entity.Property(e => e.State)
            .HasDefaultValue(0)
            .HasComment("工作状态")
            .HasColumnName("state");
        entity.Property(e => e.SystemParamters)
            .HasComment("系统参数")
            .HasColumnName("system_paramters");
        entity.Property(e => e.TeamId)
            .HasComment("团队id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.WorkflowDesignId)
            .HasComment("流程设计id")
            .HasColumnName("workflow_design_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AppWorkflowHistoryEntity> modelBuilder);
}
