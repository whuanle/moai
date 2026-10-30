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
    /// 是否支持视觉（图片输入），图片理解等场景按此筛选.
    /// </summary>
    public bool SupportsVision { get; set; }

    /// <summary>
    /// 是否来自团队自有渠道（false=系统公开或授权模型）.
    /// </summary>
    public bool IsTeamChannel { get; set; }
}
