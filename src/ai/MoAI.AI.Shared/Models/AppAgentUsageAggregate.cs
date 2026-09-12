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
}
