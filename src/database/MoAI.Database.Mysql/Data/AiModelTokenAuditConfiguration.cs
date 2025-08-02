using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 统计不同模型的token使用量，该表不是实时刷新的.
/// </summary>
public partial class AiModelTokenAuditConfiguration : IEntityTypeConfiguration<AiModelTokenAuditEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelTokenAuditEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("ai_model_token_audit", tb => tb.HasComment("统计不同模型的token使用量，该表不是实时刷新的"));

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.CompletionTokens)
            .HasComment("完成数量")
            .HasColumnType("int(11)")
            .HasColumnName("completion_tokens");
        entity.Property(e => e.Count)
            .HasComment("调用次数")
            .HasColumnType("int(11)")
            .HasColumnName("count");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("创建时间")
            .HasColumnType("datetime")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人")
            .HasColumnType("int(11)")
            .HasColumnName("create_user_id");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnType("bigint(20)")
            .HasColumnName("is_deleted");
        entity.Property(e => e.ModelId)
            .HasComment("模型id")
            .HasColumnType("int(11)")
            .HasColumnName("model_id");
        entity.Property(e => e.PromptTokens)
            .HasComment("输入数量")
            .HasColumnType("int(11)")
            .HasColumnName("prompt_tokens");
        entity.Property(e => e.TotalTokens)
            .HasComment("总数量")
            .HasColumnType("int(11)")
            .HasColumnName("total_tokens");
        entity.Property(e => e.UpdateTime)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("current_timestamp()")
            .HasComment("更新时间")
            .HasColumnType("datetime")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人")
            .HasColumnType("int(11)")
            .HasColumnName("update_user_id");
        entity.Property(e => e.UseriId)
            .HasComment("用户id")
            .HasColumnType("int(11)")
            .HasColumnName("useri_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelTokenAuditEntity> modelBuilder);
}
