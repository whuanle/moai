namespace MoAI.App.Queries.Responses;

/// <summary>
/// Agent 上下文压缩生效默认参数（系统默认，应用配置页输入框预填值）.
/// </summary>
public class QueryAgentCompactionDefaultsResponse
{
    /// <summary>
    /// 工具结果折叠触发消息数.
    /// </summary>
    public int ToolResultTriggerMessages { get; init; }

    /// <summary>
    /// 摘要触发 token.
    /// </summary>
    public int SummarizeTriggerTokens { get; init; }

    /// <summary>
    /// 滑动窗口保留轮数.
    /// </summary>
    public int PreserveTurns { get; init; }

    /// <summary>
    /// 紧急截断 token 上限.
    /// </summary>
    public int HardTokenLimit { get; init; }

    /// <summary>
    /// 是否启用 LLM 摘要压缩.
    /// </summary>
    public bool EnableSummarization { get; init; }

    /// <summary>
    /// 摘要模型 id，空表示回落应用对话模型.
    /// </summary>
    public string SummarizerModelId { get; init; } = string.Empty;
}
