using MoAI.AIChannel.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 同步探测到的候选模型.
/// </summary>
public class SyncAIModelCandidateItem
{
    /// <summary>
    /// 模型元数据（未命中内置目录时仅含 modelId 与 name）.
    /// </summary>
    public AIChannelModelMeta Meta { get; init; } = default!;

    /// <summary>
    /// 是否命中内置模型目录（models.json），命中时元数据完整.
    /// </summary>
    public bool Recognized { get; init; }

    /// <summary>
    /// 该模型是否已存在于当前渠道下（已存在时导入会跳过）.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// 模型类型（服务端按目录与规则推导，用于前端展示与筛选）.
    /// </summary>
    public string ModelKind { get; init; } = default!;
}
