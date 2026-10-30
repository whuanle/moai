namespace MoAI.App.Queries.Responses;

/// <summary>
/// 长期记忆默认提示词与平台记忆设置回显（应用未配置 memory.prompt 时生效的平台默认值，前端展示与占位）.
/// </summary>
public class QueryAgentMemoryDefaultsResponse
{
    /// <summary>
    /// 默认记忆提示词（追加为记忆工具描述，指导 AI 如何管理与使用记忆）.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 平台是否已开启长期记忆（MEMORY_ENABLED 总开关；关闭时应用不可开启记忆）.
    /// </summary>
    public bool PlatformEnabled { get; init; }

    /// <summary>
    /// 平台统一向量化模型名称（应用记忆与全局记忆共用；未配置时为空串）.
    /// </summary>
    public string EmbeddingModelName { get; init; } = string.Empty;

    /// <summary>
    /// 平台统一向量维度（1~2000，默认 1024）.
    /// </summary>
    public int EmbeddingDimensions { get; init; }
}
