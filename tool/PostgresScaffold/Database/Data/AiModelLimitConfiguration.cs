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
/// ai模型额度规则，一行=某主体在一个重置周期内的tokens上限，只能用于系统模型.
/// </summary>
internal partial class AiModelLimitConfiguration : IEntityTypeConfiguration<AiModelLimitEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelLimitEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_ai_model_limit_primary");

        entity.ToTable("ai_model_limit", tb => tb.HasComment("ai模型额度规则，一行=某主体在一个重置周期内的tokens上限，只能用于系统模型"));

        entity.HasIndex(e => new { e.ModelId, e.TeamId, e.PeriodUnit }, "idx_ai_model_limit_scope_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.HasIndex(e => e.TeamId, "idx_ai_model_limit_team_id_index");

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
        entity.Property(e => e.ExpirationTime)
            .HasComment("规则本身的有效期，null=长期有效；注意区别于重置周期，到期后规则整体失效")
            .HasColumnName("expiration_time");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LimitValue)
            .HasComment("每个重置周期内的tokens上限")
            .HasColumnName("limit_value");
        entity.Property(e => e.ModelId)
            .HasComment("模型id")
            .HasColumnName("model_id");
        entity.Property(e => e.PeriodUnit)
            .HasComment("重置周期单位：0=不重置(总量一次性) 1=小时 2=天 3=周 4=月")
            .HasColumnName("period_unit");
        entity.Property(e => e.PeriodValue)
            .HasDefaultValue(1)
            .HasComment("重置周期长度，与period_unit配合：每8小时=(8,1)、每天=(1,2)、每30天=(30,2)；period_unit=0时本列无效")
            .HasColumnName("period_value");
        entity.Property(e => e.TeamId)
            .HasComment("额度主体团队id，0=不限定团队")
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

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelLimitEntity> modelBuilder);
}
