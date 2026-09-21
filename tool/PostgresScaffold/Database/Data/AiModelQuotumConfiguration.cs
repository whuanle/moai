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
/// ai模型额度余额，按规则维度记录当前周期已消耗与剩余，剩余=total_limit-used_tokens.
/// </summary>
internal partial class AiModelQuotumConfiguration : IEntityTypeConfiguration<AiModelQuotumEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelQuotumEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_ai_model_quota_primary");

        entity.ToTable("ai_model_quota", tb => tb.HasComment("ai模型额度余额，按规则维度记录当前周期已消耗与剩余，剩余=total_limit-used_tokens"));

        entity.HasIndex(e => e.LimitId, "idx_ai_model_quota_limit_id_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.PeriodEnd, "idx_ai_model_quota_period_end_index");

        entity.HasIndex(e => e.TeamId, "idx_ai_model_quota_team_id_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LastResetTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("最近一次重置时间，null=创建后从未重置")
            .HasColumnName("last_reset_time");
        entity.Property(e => e.LimitId)
            .HasComment("逻辑关联ai_model_limit.id，每条启用中的规则对应一行余额")
            .HasColumnName("limit_id");
        entity.Property(e => e.ModelId)
            .HasComment("模型id，冗余自规则，便于直接按模型查询")
            .HasColumnName("model_id");
        entity.Property(e => e.PeriodEnd)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("当前重置周期终点，定时任务扫描该列<=now的行执行重置")
            .HasColumnName("period_end");
        entity.Property(e => e.PeriodStart)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("当前重置周期起点")
            .HasColumnName("period_start");
        entity.Property(e => e.TeamId)
            .HasComment("额度主体团队id，冗余自规则，0=模型全局/个人额度")
            .HasColumnName("team_id");
        entity.Property(e => e.TotalLimit)
            .HasComment("当前周期总额度，规则变更时同步快照到本列")
            .HasColumnName("total_limit");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UsedTokens)
            .HasComment("当前周期已消耗tokens")
            .HasColumnName("used_tokens");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelQuotumEntity> modelBuilder);
}
