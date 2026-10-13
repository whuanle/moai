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
/// 团队定时器执行记录：一次触发一行，失败重试在同一行内累计尝试次数.
/// </summary>
internal partial class TeamTimerExecutionConfiguration : IEntityTypeConfiguration<TeamTimerExecutionEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamTimerExecutionEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_timer_execution_pkey");

        entity.ToTable("team_timer_execution", tb => tb.HasComment("团队定时器执行记录：一次触发一行，失败重试在同一行内累计尝试次数"));

        entity.HasIndex(e => e.TeamId, "idx_team_timer_execution_team_id");

        entity.HasIndex(e => new { e.TimerId, e.CreateTime }, "idx_team_timer_execution_timer_create");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppInstanceId)
            .HasMaxLength(64)
            .HasComment("流程应用实例 id，Agent 应用为 null")
            .HasColumnName("app_instance_id");
        entity.Property(e => e.AttemptCount)
            .HasComment("已尝试次数（首次执行记 1）")
            .HasColumnName("attempt_count");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.DurationMs)
            .HasComment("总耗时毫秒，未结束为 null")
            .HasColumnName("duration_ms");
        entity.Property(e => e.EndTime)
            .HasComment("结束时间，未结束为 null")
            .HasColumnName("end_time");
        entity.Property(e => e.ErrorMessage)
            .HasComment("失败原因")
            .HasColumnName("error_message");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.NextRetryTime)
            .HasComment("下次重试时间，仅待重试状态有值")
            .HasColumnName("next_retry_time");
        entity.Property(e => e.Result)
            .HasComment("应用回答文本")
            .HasColumnName("result");
        entity.Property(e => e.StartTime)
            .HasComment("首次开始执行时间，未开始为 null")
            .HasColumnName("start_time");
        entity.Property(e => e.Status)
            .HasComment("执行状态，0=等待 1=执行中 2=成功 3=失败 4=待重试")
            .HasColumnName("status");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队 id，逻辑关联 team_timer.team_id")
            .HasColumnName("team_id");
        entity.Property(e => e.TimerId)
            .HasComment("定时器 id，逻辑关联 team_timer.id")
            .HasColumnName("timer_id");
        entity.Property(e => e.TriggerType)
            .HasComment("触发方式，0=调度 1=手动")
            .HasColumnName("trigger_type");
        entity.Property(e => e.TriggerUserId)
            .HasComment("触发用户 id（手动=操作人，调度=定时器创建人）")
            .HasColumnName("trigger_user_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamTimerExecutionEntity> modelBuilder);
}
