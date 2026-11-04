using System;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// 团队可用模型项.
/// </summary>
public class TeamAvailableModelItem
{
    /// <summary>
    /// ai模型 id.
    /// </summary>
    public Guid AiModelId { get; set; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 渠道模型标识（即 ai_model.model_id）.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// 所属渠道名称.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// 渠道供应商标识.
    /// </summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>
    /// 模型类型（conversation/embedding/image-generation 等），对话类场景按 conversation 筛选.
    /// </summary>
    public string ModelKind { get; set; } = string.Empty;

    /// <summary>
    /// 是否支持视觉（图片输入），图片理解等场景按此筛选.
    /// </summary>
    public bool SupportsVision { get; set; }

    /// <summary>
    /// 是否支持工具调用（function calling）.
    /// </summary>
    public bool SupportsToolCall { get; set; }

    /// <summary>
    /// 是否支持推理（reasoning）.
    /// </summary>
    public bool SupportsReasoning { get; set; }

    /// <summary>
    /// 上下文最大 token 数；目录元数据缺失时为 0.
    /// </summary>
    public int ContextWindow { get; set; }

    /// <summary>
    /// 最大输出 token 数；目录元数据缺失时为 0.
    /// </summary>
    public int MaxOutput { get; set; }

    /// <summary>
    /// 是否来自团队自有渠道（false=系统公开或授权模型）.
    /// </summary>
    public bool IsTeamChannel { get; set; }
}
