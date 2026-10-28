namespace MoAI.App.Queries.Responses;

/// <summary>
/// 会话上下文用量响应.
/// </summary>
public class QueryAppSessionContextUsageCommandResponse
{
    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 生效对话模型名称（未配置或模型不可用为空串）.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 生效对话模型的上下文窗口上限（token），未知为 0.
    /// </summary>
    public int ContextWindow { get; set; }

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
    /// 缓存命中的输入 token 累计（模型响应 usage 上报，未上报为 0）.
    /// </summary>
    public int CachedTokens { get; set; }

    /// <summary>
    /// 当前上下文占用（最近一次模型调用的输入 token；上下文压缩后随下一轮回落）.
    /// </summary>
    public int ContextTokens { get; set; }
}
