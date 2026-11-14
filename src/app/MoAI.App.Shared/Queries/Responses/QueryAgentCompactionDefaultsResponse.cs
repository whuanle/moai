namespace MoAI.App.Queries.Responses;

/// <summary>
/// Agent 上下文压缩生效默认参数（系统默认，应用配置页预填值与自动模式换算展示）.
/// </summary>
public class QueryAgentCompactionDefaultsResponse
{
    /// <summary>
    /// 默认压缩策略：auto（按模型上下文窗口比例推导）.
    /// </summary>
    public string CompactionMode { get; init; } = MoAI.AI.AppCompactionPolicy.ModeAuto;

    /// <summary>
    /// 自动模式：工具结果折叠触发比例（占 input budget）.
    /// </summary>
    public double ToolEvictionRatio { get; init; } = MoAI.AI.AppCompactionPolicy.ToolEvictionRatio;

    /// <summary>
    /// 自动模式：LLM 摘要压缩触发比例（占 input budget）.
    /// </summary>
    public double SummarizeTriggerRatio { get; init; } = MoAI.AI.AppCompactionPolicy.SummarizeTriggerRatio;

    /// <summary>
    /// 自动模式：摘要压缩目标比例（压回该水位）.
    /// </summary>
    public double SummarizeTargetRatio { get; init; } = MoAI.AI.AppCompactionPolicy.SummarizeTargetRatio;

    /// <summary>
    /// 自动模式：紧急截断触发比例（占 input budget）.
    /// </summary>
    public double TruncationRatio { get; init; } = MoAI.AI.AppCompactionPolicy.TruncationRatio;

    /// <summary>
    /// 自定义模式：工具结果折叠触发消息数默认值.
    /// </summary>
    public int ToolResultTriggerMessages { get; init; }

    /// <summary>
    /// 自定义模式：摘要触发 token 默认值.
    /// </summary>
    public int SummarizeTriggerTokens { get; init; }

    /// <summary>
    /// 自定义模式：紧急截断 token 上限默认值.
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
