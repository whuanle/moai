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
/// 统计不同模型的token使用量，该表不是实时刷新的.
/// </summary>
internal partial class AiModelTokenAuditConfiguration : IEntityTypeConfiguration<AiModelTokenAuditEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelTokenAuditEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("idx_62988_primary");

        entity.ToTable("ai_model_token_audit", tb => tb.HasComment("统计不同模型的token使用量，该表不是实时刷新的"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.CompletionTokens)
            .HasDefaultValue(0)
            .HasComment("完成数量")
            .HasColumnName("completion_tokens");
        entity.Property(e => e.Count)
            .HasDefaultValue(0)
            .HasComment("调用次数")
            .HasColumnName("count");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasDefaultValue(0)
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
            .HasDefaultValue(0)
            .HasComment("输入数量")
            .HasColumnName("prompt_tokens");
        entity.Property(e => e.TotalTokens)
            .HasDefaultValue(0)
            .HasComment("总数量")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasDefaultValue(0)
            .HasComment("更新人")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UseriId)
            .HasComment("用户id")
            .HasColumnName("useri_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelTokenAuditEntity> modelBuilder);
}
