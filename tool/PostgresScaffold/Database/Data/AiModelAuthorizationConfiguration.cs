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
/// 授权模型给哪些团队使用，额度以团队为单位共享.
/// </summary>
internal partial class AiModelAuthorizationConfiguration : IEntityTypeConfiguration<AiModelAuthorizationEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelAuthorizationEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_ai_model_authorization_primary");

        entity.ToTable("ai_model_authorization", tb => tb.HasComment("授权模型给哪些团队使用，额度以团队为单位共享"));

        entity.HasIndex(e => new { e.AiModelId, e.TeamId }, "idx_ai_model_authorization_model_team_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.AiModelId)
            .HasComment("ai模型的id")
            .HasColumnName("ai_model_id");
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
        entity.Property(e => e.TeamId)
            .HasComment("授权团队id，该团队成员可用，团队公开应用/知识库的外部使用也归属该团队")
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

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelAuthorizationEntity> modelBuilder);
}
