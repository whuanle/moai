using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 模型.
/// </summary>
public partial class AiModelEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属渠道 id.
    /// </summary>
    public Guid ChannelId { get; set; }

    /// <summary>
    /// 模型标识，对应 models.json 中的模型 id.
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
    /// 模型类型（conversation/embedding/image-generation/transcription）.
    /// </summary>
    public string ModelKind { get; set; } = default!;

    /// <summary>
    /// 是否支持视觉（图片输入）.
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
    /// 输入模态，JSON 数组.
    /// </summary>
    public string? ModalitiesInput { get; set; }

    /// <summary>
    /// 输出模态，JSON 数组.
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

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 创建人 id.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 更新人 id.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
