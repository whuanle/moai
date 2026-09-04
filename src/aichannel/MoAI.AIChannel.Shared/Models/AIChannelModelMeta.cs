namespace MoAI.AIChannel.Models;

/// <summary>
/// 模型元数据，映射自 opencode models.json 中的模型对象.
/// </summary>
public class AIChannelModelMeta
{
    /// <summary>
    /// 模型标识，例如 gpt-4o、deepseek/deepseek-v4-flash.
    /// </summary>
    public string ModelId { get; set; } = default!;

    /// <summary>
    /// 模型类型（conversation/embedding/image-generation/transcription/video-generation），为空时由服务端自动推导.
    /// </summary>
    public string? ModelKind { get; set; }

    /// <summary>
    /// 展示名称，例如 gpt-4o.
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
    /// 是否支持视觉，为空时根据输入/输出模态推导.
    /// </summary>
    public bool? SupportsVision { get; set; }

    /// <summary>
    /// 是否支持附件上传.
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
    /// 输入模态，例如 ["text","image"].
    /// </summary>
    public List<string>? InputModalities { get; set; }

    /// <summary>
    /// 输出模态，例如 ["text"].
    /// </summary>
    public List<string>? OutputModalities { get; set; }

    /// <summary>
    /// 是否开放权重.
    /// </summary>
    public bool OpenWeights { get; set; }

    /// <summary>
    /// 上下文最大 token 数.
    /// </summary>
    public int? ContextWindow { get; set; }

    /// <summary>
    /// 最大输出 token 数.
    /// </summary>
    public int? MaxOutput { get; set; }

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
}
