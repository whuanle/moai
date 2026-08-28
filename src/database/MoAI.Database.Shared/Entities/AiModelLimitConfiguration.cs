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
/// ai模型使用量限制，只能用于系统模型.
/// </summary>
internal partial class AiModelLimitConfiguration : IEntityTypeConfiguration<AiModelLimitEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelLimitEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_65567_primary");

        entity.ToTable("ai_model_limit", tb => tb.HasComment("ai模型使用量限制，只能用于系统模型"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
            .HasComment("创建人")
            .HasColumnName("create_user_id");
        entity.Property(e => e.ExpirationTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("过期时间")
            .HasColumnName("expiration_time");
        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("'0'::bigint")
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.LimitValue)
            .HasComment("限制值")
            .HasColumnName("limit_value");
        entity.Property(e => e.ModelId)
            .HasComment("模型id")
            .HasColumnName("model_id");
        entity.Property(e => e.RuleType)
            .HasDefaultValue(0)
            .HasComment("限制的规则类型,每天/总额/有效期")
            .HasColumnName("rule_type");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UserId)
            .HasComment("用户id")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelLimitEntity> modelBuilder);
}
