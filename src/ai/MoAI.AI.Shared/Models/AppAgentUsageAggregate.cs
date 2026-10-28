namespace MoAI.AI.Models;

/// <summary>
/// Agent 会话用量聚合（Redis 热态，flush 时回写 app_agent_session）.
/// </summary>
public sealed class AppAgentUsageAggregate
{
    /// <summary>
    /// 输入 token 累计.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出 token 累计.
    /// </summary>
    public int OutTokens { get; set; }

    /// <summary>
    /// token 累计总数.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 缓存命中的输入 token 累计（模型响应 usage 的 cached_tokens，未上报为 0）.
    /// </summary>
    public int CachedTokens { get; set; }

    /// <summary>
    /// 最近一次模型调用的输入 token（即当前上下文真实占用；上下文压缩后随下一轮回落）.
    /// </summary>
    public int LastPromptTokens { get; set; }
}
