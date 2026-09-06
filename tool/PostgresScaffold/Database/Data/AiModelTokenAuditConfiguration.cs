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
/// 统计不同模型的token使用量，该表不是实时刷新的，按模型+团队+用户+业务来源维度累加.
/// </summary>
internal partial class AiModelTokenAuditConfiguration : IEntityTypeConfiguration<AiModelTokenAuditEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelTokenAuditEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_ai_model_token_audit_primary");

        entity.ToTable("ai_model_token_audit", tb => tb.HasComment("统计不同模型的token使用量，该表不是实时刷新的，按模型+团队+用户+业务来源维度累加"));

        entity.HasIndex(e => new { e.ModelId, e.TeamId, e.UserId, e.UseType, e.UseResourceId }, "idx_ai_model_token_audit_dim_uindex")
            .IsUnique()
            .HasFilter("(is_deleted = 0)");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CompletionTokens)
            .HasComment("完成数量")
            .HasColumnName("completion_tokens");
        entity.Property(e => e.Count)
            .HasComment("调用次数")
            .HasColumnName("count");
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
        entity.Property(e => e.ModelId)
            .HasComment("模型id")
            .HasColumnName("model_id");
        entity.Property(e => e.PromptTokens)
            .HasComment("输入数量")
            .HasColumnName("prompt_tokens");
        entity.Property(e => e.TeamId)
            .HasComment("额度归属团队id，0=个人直接使用，>0=通过该团队资源使用（含公开应用/知识库被外部用户使用）")
            .HasColumnName("team_id");
        entity.Property(e => e.TotalTokens)
            .HasComment("总数量")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UseResourceId)
            .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'::uuid")
            .HasComment("消耗来源资源id，use_type=1时为应用id、=2时为知识库id、=3时为工作流id；use_type=0时为0")
            .HasColumnName("use_resource_id");
        entity.Property(e => e.UseType)
            .HasComment("消耗来源类型：0=个人会话 1=应用 2=知识库 3=工作流，新增类型依次递增")
            .HasColumnName("use_type");
        entity.Property(e => e.UserId)
            .HasComment("实际使用者用户id")
            .HasColumnName("user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelTokenAuditEntity> modelBuilder);
}
