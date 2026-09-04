using System;
using System.Collections.Generic;
using System.Linq;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Extensions;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 将 models.json 模型元数据映射为 AI 模型实体，并推导模型类型与是否支持视觉.
/// </summary>
public static class AIModelMetaMapper
{
    /// <summary>
    /// 将元数据应用到实体（不设置渠道、审计、启用等外部属性）.
    /// </summary>
    /// <param name="entity">实体.</param>
    /// <param name="meta">模型元数据.</param>
    public static AiModelEntity Apply(AiModelEntity entity, AIChannelModelMeta meta)
    {
        entity.ModelId = meta.ModelId;
        entity.Name = meta.Name;
        entity.Description = meta.Description;
        entity.Family = meta.Family;
        entity.SupportsAttachments = meta.SupportsAttachments;
        entity.SupportsReasoning = meta.SupportsReasoning;
        entity.SupportsToolCall = meta.SupportsToolCall;
        entity.SupportsStructuredOutput = meta.SupportsStructuredOutput;
        entity.SupportsTemperature = meta.SupportsTemperature;
        entity.KnowledgeCutoff = meta.KnowledgeCutoff;
        entity.ReleaseDate = meta.ReleaseDate;
        entity.LastUpdated = meta.LastUpdated;
        entity.OpenWeights = meta.OpenWeights;
        entity.ContextWindow = meta.ContextWindow ?? 0;
        entity.MaxOutput = meta.MaxOutput ?? 0;
        entity.CostInput = meta.CostInput;
        entity.CostOutput = meta.CostOutput;
        entity.CostCacheRead = meta.CostCacheRead;

        entity.ModelKind = string.IsNullOrWhiteSpace(meta.ModelKind)
            ? DeriveModelKind(meta)
            : meta.ModelKind;
        entity.SupportsVision = meta.SupportsVision
            ?? (meta.InputModalities?.Contains("image", StringComparer.OrdinalIgnoreCase) == true
                || meta.OutputModalities?.Contains("image", StringComparer.OrdinalIgnoreCase) == true);

        entity.ModalitiesInput = meta.InputModalities?.ToJsonString();
        entity.ModalitiesOutput = meta.OutputModalities?.ToJsonString();

        return entity;
    }

    /// <summary>
    /// 推导模型类型：结合 family / 模型 id / 输入输出模态，识别文本、向量、语音、生图、视频等.
    /// </summary>
    /// <param name="meta">模型元数据.</param>
    /// <returns>返回 <see cref="AIModelKind"/> 对应的字符串.</returns>
    public static string DeriveModelKind(AIChannelModelMeta meta)
    {
        var family = meta.Family?.ToLowerInvariant() ?? string.Empty;
        var modelId = meta.ModelId?.ToLowerInvariant() ?? string.Empty;
        var outputs = (meta.OutputModalities ?? new List<string>())
            .Select(x => x.ToLowerInvariant())
            .ToList();

        var hasText = outputs.Contains("text");

        // 向量/嵌入模型：family 或 名称/id 含 embedding，或输出模态为 embedding/vector
        if (family.Contains("embedding", StringComparison.OrdinalIgnoreCase)
            || modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase)
            || outputs.Any(x => x == "embedding" || x == "vector"))
        {
            return AIChannelMetaMapperNames.Embedding;
        }

        // 语音模型：输出为 audio/speech，或 id/family 含 whisper/tts/audio/speech/voice
        if (outputs.Any(x => x == "audio" || x == "speech")
            || family.Contains("whisper", StringComparison.OrdinalIgnoreCase)
            || family.Contains("audio", StringComparison.OrdinalIgnoreCase)
            || modelId.Contains("whisper", StringComparison.OrdinalIgnoreCase)
            || modelId.Contains("tts", StringComparison.OrdinalIgnoreCase)
            || modelId.Contains("audio", StringComparison.OrdinalIgnoreCase)
            || modelId.Contains("speech", StringComparison.OrdinalIgnoreCase)
            || modelId.Contains("voice", StringComparison.OrdinalIgnoreCase))
        {
            return AIChannelMetaMapperNames.Transcription;
        }

        // 视频生成模型：输出为 video，或 id 含 video（且非纯文本输出）
        if (outputs.Contains("video") || (modelId.Contains("video", StringComparison.OrdinalIgnoreCase) && !hasText))
        {
            return AIChannelMetaMapperNames.VideoGeneration;
        }

        // 生图模型：输出为 image 且无 text，或 id 含 image（且非纯文本输出）
        var hasImage = outputs.Any(x => x == "image" || x == "image-generation");
        if ((hasImage && !hasText)
            || (modelId.Contains("image", StringComparison.OrdinalIgnoreCase) && !hasText))
        {
            return AIChannelMetaMapperNames.ImageGeneration;
        }

        // 纯文本 / 对话模型（含视觉输入但输出为文本的模型）
        return AIChannelMetaMapperNames.Conversation;
    }
}

/// <summary>
/// 模型类型字符串常量.
/// </summary>
internal static class AIChannelMetaMapperNames
{
    internal const string Conversation = "conversation";
    internal const string Embedding = "embedding";
    internal const string ImageGeneration = "image-generation";
    internal const string Transcription = "transcription";
    internal const string VideoGeneration = "video-generation";
}
