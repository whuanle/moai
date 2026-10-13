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
/// 团队定时器：绑定应用按 cron 定时无头执行.
/// </summary>
internal partial class TeamTimerConfiguration : IEntityTypeConfiguration<TeamTimerEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TeamTimerEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("team_timer_pkey");

        entity.ToTable("team_timer", tb => tb.HasComment("团队定时器：绑定应用按 cron 定时无头执行"));

        entity.HasIndex(e => e.TeamId, "idx_team_timer_team_id");

        entity.HasIndex(e => new { e.TeamId, e.Name, e.IsDeleted }, "ux_team_timer_team_name_live_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AppId)
            .HasComment("绑定的应用 id，逻辑关联 app.id")
            .HasColumnName("app_id");
        entity.Property(e => e.AppType)
            .HasComment("绑定时的应用类型快照，0=Agent 1=流程")
            .HasColumnName("app_type");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.CronExpression)
            .HasMaxLength(100)
            .HasComment("调度 cron 表达式（UTC，5 段标准格式）")
            .HasColumnName("cron_expression");
        entity.Property(e => e.Description)
            .HasMaxLength(255)
            .HasDefaultValueSql("''::character varying")
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.InputContent)
            .HasComment("每次执行时作为提问发送给应用的内容")
            .HasColumnName("input_content");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除，0=未删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.IsEnabled)
            .HasComment("是否启用调度")
            .HasColumnName("is_enabled");
        entity.Property(e => e.LastRunTime)
            .HasComment("最近一次触发时间，未执行过为 null")
            .HasColumnName("last_run_time");
        entity.Property(e => e.MaxRetryCount)
            .HasComment("失败重试次数（不含首次执行）")
            .HasColumnName("max_retry_count");
        entity.Property(e => e.Name)
            .HasMaxLength(100)
            .HasComment("定时器名称，团队内唯一")
            .HasColumnName("name");
        entity.Property(e => e.RetryIntervalSeconds)
            .HasDefaultValue(60)
            .HasComment("重试间隔秒数")
            .HasColumnName("retry_interval_seconds");
        entity.Property(e => e.TeamId)
            .HasComment("所属团队 id，逻辑关联 team.id")
            .HasColumnName("team_id");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamTimerEntity> modelBuilder);
}
