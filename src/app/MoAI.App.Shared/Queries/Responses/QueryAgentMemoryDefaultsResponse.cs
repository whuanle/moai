namespace MoAI.App.Queries.Responses;

/// <summary>
/// 长期记忆默认提示词（应用未配置 memory.prompt 时生效的平台默认值，前端展示与占位）.
/// </summary>
public class QueryAgentMemoryDefaultsResponse
{
    /// <summary>
    /// 默认记忆提示词（追加为记忆工具描述，指导 AI 如何管理与使用记忆）.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;
}
