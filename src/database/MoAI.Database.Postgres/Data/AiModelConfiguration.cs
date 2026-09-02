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
/// 模型.
/// </summary>
internal partial class AiModelConfiguration : IEntityTypeConfiguration<AiModelEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiModelEntity> builder)
    {
        var entity = builder;
        entity.HasKey(e => e.Id).HasName("ai_model_pkey");

        entity.ToTable("ai_model", tb => tb.HasComment("模型"));

        entity.HasIndex(e => new { e.ChannelId, e.ModelId }, "idx_ai_model_channel_model");

        entity.HasIndex(e => e.ModelKind, "idx_ai_model_model_kind");

        entity.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()")
            .HasComment("id")
            .HasColumnName("id");
        entity.Property(e => e.ChannelId)
            .HasComment("所属渠道 id")
            .HasColumnName("channel_id");
        entity.Property(e => e.ContextWindow)
            .HasComment("上下文最大 token 数")
            .HasColumnName("context_window");
        entity.Property(e => e.CostCacheRead)
            .HasPrecision(18, 6)
            .HasComment("缓存读单价")
            .HasColumnName("cost_cache_read");
        entity.Property(e => e.CostInput)
            .HasPrecision(18, 6)
            .HasComment("输入单价")
            .HasColumnName("cost_input");
        entity.Property(e => e.CostOutput)
            .HasPrecision(18, 6)
            .HasComment("输出单价")
            .HasColumnName("cost_output");
        entity.Property(e => e.CreateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("创建时间")
            .HasColumnName("create_time");
        entity.Property(e => e.CreateUserId)
            .HasComment("创建人 id")
            .HasColumnName("create_user_id");
        entity.Property(e => e.Description)
            .HasMaxLength(2000)
            .HasComment("描述")
            .HasColumnName("description");
        entity.Property(e => e.Enabled)
            .HasDefaultValue(true)
            .HasComment("是否启用")
            .HasColumnName("enabled");
        entity.Property(e => e.Family)
            .HasMaxLength(100)
            .HasComment("模型族")
            .HasColumnName("family");
        entity.Property(e => e.IsDeleted)
            .HasComment("软删除")
            .HasColumnName("is_deleted");
        entity.Property(e => e.KnowledgeCutoff)
            .HasMaxLength(50)
            .HasComment("知识截止时间")
            .HasColumnName("knowledge_cutoff");
        entity.Property(e => e.LastUpdated)
            .HasMaxLength(50)
            .HasComment("最近更新时间")
            .HasColumnName("last_updated");
        entity.Property(e => e.MaxOutput)
            .HasComment("最大输出 token 数")
            .HasColumnName("max_output");
        entity.Property(e => e.ModalitiesInput)
            .HasMaxLength(500)
            .HasComment("输入模态，JSON 数组")
            .HasColumnName("modalities_input");
        entity.Property(e => e.ModalitiesOutput)
            .HasMaxLength(500)
            .HasComment("输出模态，JSON 数组")
            .HasColumnName("modalities_output");
        entity.Property(e => e.ModelId)
            .HasMaxLength(200)
            .HasComment("模型标识，对应 models.json 中的模型 id")
            .HasColumnName("model_id");
        entity.Property(e => e.ModelKind)
            .HasMaxLength(30)
            .HasComment("模型类型（conversation/embedding/image-generation/transcription）")
            .HasColumnName("model_kind");
        entity.Property(e => e.Name)
            .HasMaxLength(200)
            .HasComment("模型名称")
            .HasColumnName("name");
        entity.Property(e => e.OpenWeights)
            .HasComment("是否开放权重")
            .HasColumnName("open_weights");
        entity.Property(e => e.ReleaseDate)
            .HasMaxLength(50)
            .HasComment("发布日期")
            .HasColumnName("release_date");
        entity.Property(e => e.SupportsAttachments)
            .HasComment("是否支持附件")
            .HasColumnName("supports_attachments");
        entity.Property(e => e.SupportsReasoning)
            .HasComment("是否支持推理")
            .HasColumnName("supports_reasoning");
        entity.Property(e => e.SupportsStructuredOutput)
            .HasComment("是否支持结构化输出")
            .HasColumnName("supports_structured_output");
        entity.Property(e => e.SupportsTemperature)
            .HasComment("是否支持温度参数")
            .HasColumnName("supports_temperature");
        entity.Property(e => e.SupportsToolCall)
            .HasComment("是否支持功能调用")
            .HasColumnName("supports_tool_call");
        entity.Property(e => e.SupportsVision)
            .HasComment("是否支持视觉（图片输入）")
            .HasColumnName("supports_vision");
        entity.Property(e => e.UpdateTime)
            .HasDefaultValueSql("timezone('utc'::text, now())")
            .HasComment("更新时间")
            .HasColumnName("update_time");
        entity.Property(e => e.UpdateUserId)
            .HasComment("更新人 id")
            .HasColumnName("update_user_id");

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AiModelEntity> modelBuilder);
}
