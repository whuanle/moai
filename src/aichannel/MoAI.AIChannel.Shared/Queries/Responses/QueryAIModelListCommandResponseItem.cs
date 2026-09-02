using System;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// QueryAIModelListCommandResponseItem.
/// </summary>
public class QueryAIModelListCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属渠道 id.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <summary>
    /// 模型标识.
    /// </summary>
    public string ModelId { get; set; } = default!;

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 模型族.
    /// </summary>
    public string? Family { get; set; }

    /// <summary>
    /// 模型类型.
    /// </summary>
    public string ModelKind { get; set; } = default!;

    /// <summary>
    /// 是否支持视觉.
    /// </summary>
    public bool SupportsVision { get; set; }

    /// <summary>
    /// 是否支持附件.
    /// </summary>
    public bool SupportsAttachments { get; set; }

    /// <summary>
    /// 是否支持推理.
    /// </summary>
    public bool SupportsReasoning { get; set; }

    /// <summary>
    /// 是否支持功能调用.
    /// </summary>
    public bool SupportsToolCall { get; set; }

    /// <summary>
    /// 是否支持结构化输出.
    /// </summary>
    public bool SupportsStructuredOutput { get; set; }

    /// <summary>
    /// 是否支持温度参数.
    /// </summary>
    public bool SupportsTemperature { get; set; }

    /// <summary>
    /// 上下文最大 token 数.
    /// </summary>
    public int ContextWindow { get; set; }

    /// <summary>
    /// 最大输出 token 数.
    /// </summary>
    public int MaxOutput { get; set; }

    /// <summary>
    /// 输入模态.
    /// </summary>
    public string? ModalitiesInput { get; set; }

    /// <summary>
    /// 输出模态.
    /// </summary>
    public string? ModalitiesOutput { get; set; }

    /// <summary>
    /// 知识截止时间.
    /// </summary>
    public string? KnowledgeCutoff { get; set; }

    /// <summary>
    /// 发布日期.
    /// </summary>
    public string? ReleaseDate { get; set; }

    /// <summary>
    /// 最近更新时间.
    /// </summary>
    public string? LastUpdated { get; set; }

    /// <summary>
    /// 是否开放权重.
    /// </summary>
    public bool OpenWeights { get; set; }

    /// <summary>
    /// 输入单价.
    /// </summary>
    public decimal? CostInput { get; set; }

    /// <summary>
    /// 输出单价.
    /// </summary>
    public decimal? CostOutput { get; set; }

    /// <summary>
    /// 缓存读单价.
    /// </summary>
    public decimal? CostCacheRead { get; set; }

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; set; }
}
