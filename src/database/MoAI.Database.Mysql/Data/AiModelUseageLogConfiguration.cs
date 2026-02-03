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
/// 模型使用日志,记录每次请求使用记录.
/// </summary>
internal partial class AiModelUseageLogConfiguration : IEntityTypeConfiguration<AiModelUseageLogEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelUseageLogEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("PRIMARY");

        entity.ToTable("ai_model_useage_log", tb => tb.HasComment("模型使用日志,记录每次请求使用记录"));

        entity.HasIndex(e => e.Channel, "ai_model_useage_log_channel_index");

        entity.Property(e => e.Id)
            .HasComment("id")
            .HasColumnType("int(11)")
            .HasColumnName("id");
        entity.Property(e => e.Channel)
            .HasMaxLength(30)
            .HasDefaultValueSql("'-'")
            .HasComment("渠道")
            .HasColumnName("channel");
        entity.Property(e => e.CompletionTokens)
            .HasComment("完成数量")
            .HasColumnType("int(11)")
            .HasColumnName("completion_tokens");
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
            .HasDefaultValueSql("utc_timestamp()")
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

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelUseageLogEntity> modelBuilder);
}
