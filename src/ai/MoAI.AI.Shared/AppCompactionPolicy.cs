namespace MoAI.AI;

/// <summary>
/// 应用上下文压缩策略契约：模式标记与自动模式的窗口比例阈值.
/// <para>
/// 自动模式（compactionMode 缺省值）阈值按「所选对话模型的上下文窗口」动态推导：
/// input budget = ContextWindow − MaxOutput（与库内 ContextWindowCompactionStrategy 同口径），
/// 工具结果折叠 50% → LLM 摘要 80% 触发、压回 50%（大步长低频压缩，保持 prompt cache 前缀稳定）→ 紧急截断 95% 兜底。
/// 模型窗口元数据缺失时回落到 legacy 固定值（与历史版本行为一致）。
/// </para>
/// </summary>
public static class AppCompactionPolicy
{
    /// <summary>压缩策略：自动（按模型上下文窗口比例推导阈值，推荐）.</summary>
    public const string ModeAuto = "auto";

    /// <summary>压缩策略：自定义（按 execution_settings 中的绝对值运行）.</summary>
    public const string ModeCustom = "custom";

    /// <summary>自动模式：工具结果折叠触发比例（占 input budget，零 LLM 成本的微压缩，先行于摘要）.</summary>
    public const double ToolEvictionRatio = 0.5;

    /// <summary>自动模式：LLM 摘要压缩触发比例（占 input budget，约等于 Claude Code ~83% / Codex 90% clamp 的折中）.</summary>
    public const double SummarizeTriggerRatio = 0.8;

    /// <summary>自动模式：摘要压缩目标比例（压缩后回落到该水位，低频大步长，避免高频压缩风暴）.</summary>
    public const double SummarizeTargetRatio = 0.5;

    /// <summary>自动模式：紧急截断触发比例（占 input budget，最后一道安全网）.</summary>
    public const double TruncationRatio = 0.95;

    /// <summary>模型窗口元数据缺失时的兜底：工具结果折叠触发消息数（legacy 默认）.</summary>
    public const int FallbackToolResultTriggerMessages = 16;

    /// <summary>模型窗口元数据缺失时的兜底：摘要触发 token（legacy 默认）.</summary>
    public const int FallbackSummarizeTriggerTokens = 16000;

    /// <summary>模型窗口元数据缺失时的兜底：紧急截断 token 上限（legacy 默认）.</summary>
    public const int FallbackHardTokenLimit = 64000;

    /// <summary>自定义模式：工具结果折叠触发消息数下限.</summary>
    public const int MinToolResultTriggerMessages = 1;

    /// <summary>自定义模式：token 类阈值下限.</summary>
    public const int MinTokenLimit = 200;

    /// <summary>自定义模式：token 类阈值上限（与前端输入框上限一致）.</summary>
    public const int MaxTokenLimit = 4000000;

    /// <summary>由 input budget 计算工具结果折叠触发 token.</summary>
    /// <param name="inputBudget">模型压缩可用输入预算（ContextWindow − MaxOutput）.</param>
    /// <returns>工具结果折叠触发 token 数.</returns>
    public static int ToolEvictionTokens(int inputBudget) => (int)(inputBudget * ToolEvictionRatio);

    /// <summary>由 input budget 计算摘要触发 token.</summary>
    /// <param name="inputBudget">模型压缩可用输入预算（ContextWindow − MaxOutput）.</param>
    /// <returns>摘要压缩触发 token 数.</returns>
    public static int SummarizeTriggerTokens(int inputBudget) => (int)(inputBudget * SummarizeTriggerRatio);

    /// <summary>由 input budget 计算摘要压缩目标 token.</summary>
    /// <param name="inputBudget">模型压缩可用输入预算（ContextWindow − MaxOutput）.</param>
    /// <returns>摘要压缩目标 token 数.</returns>
    public static int SummarizeTargetTokens(int inputBudget) => (int)(inputBudget * SummarizeTargetRatio);

    /// <summary>由 input budget 计算紧急截断触发 token.</summary>
    /// <param name="inputBudget">模型压缩可用输入预算（ContextWindow − MaxOutput）.</param>
    /// <returns>紧急截断触发 token 数.</returns>
    public static int TruncationTokens(int inputBudget) => (int)(inputBudget * TruncationRatio);
}
