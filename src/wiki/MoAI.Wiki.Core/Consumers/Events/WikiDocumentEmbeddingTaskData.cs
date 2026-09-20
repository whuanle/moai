using System;
using System.Collections.Generic;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// 知识库文档工作流任务数据：可选 AI 切割 → 可选元数据生成 → 可选向量化.
/// </summary>
public class WikiDocumentEmbeddingTaskData
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 是否对原文切片内容向量化.
    /// </summary>
    public bool IsEmbedSourceText { get; init; }

    /// <summary>
    /// 是否对元数据向量化.
    /// </summary>
    public bool IsEmbedMetadata { get; init; }

    /// <summary>
    /// AI 智能切割使用的对话模型 id；非空时任务先对该文档执行 AI 切割（需已提取内容），批量工作流用.
    /// </summary>
    public Guid AiPartitionModelId { get; init; }

    /// <summary>
    /// AI 切割提示词模板，为空使用内置默认；仅在 <see cref="AiPartitionModelId"/> 非空时生效.
    /// </summary>
    public string? AiPartitionPromptTemplate { get; init; }

    /// <summary>
    /// 元数据生成使用的对话模型 id；非空时任务先对该文档全部切片替换式生成元数据（批量工作流用，单文档手动触发不传）.
    /// </summary>
    public Guid MetadataModelId { get; init; }

    /// <summary>
    /// 元数据生成策略（可多选）；为空或空集合时生成全套元数据，仅在 <see cref="MetadataModelId"/> 非空时生效.
    /// </summary>
    public List<MetadataGenerationStrategy>? MetadataStrategyTypes { get; init; }
}
