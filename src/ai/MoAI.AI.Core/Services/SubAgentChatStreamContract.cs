namespace MoAI.AI.Services;

/// <summary>
/// 子智能体对话执行事件种类.
/// </summary>
public enum SubAgentChatEventKind
{
    /// <summary>派发开始（子智能体独立上下文启动）.</summary>
    Started,

    /// <summary>子智能体正文增量片段.</summary>
    Text,

    /// <summary>子智能体发起工具调用.</summary>
    ToolCall,

    /// <summary>子智能体工具调用完成（携带截断后的结果摘要）.</summary>
    ToolResult,

    /// <summary>子智能体任务完成（携带最终结论）.</summary>
    Completed,

    /// <summary>子任务超时（携带已产出内容）.</summary>
    Timeout,

    /// <summary>子智能体执行失败.</summary>
    Failed,
}

/// <summary>
/// 子智能体对话流式契约常量.
/// </summary>
public static class SubAgentChatStreamContract
{
    /// <summary>
    /// AG-UI 自定义事件名.
    /// </summary>
    public const string EventName = "moai.subagent";

    /// <summary>
    /// 过程负载的 DataContent 媒体类型.
    /// </summary>
    public const string DataMediaType = "application/vnd.moai.subagent+json";
}

/// <summary>
/// 子智能体对话执行事件（中立负载）：由 <see cref="SubAgentInvoker"/> 在子会话流式执行中发布，
/// 经 <see cref="SubAgentProgressChannel"/> 合并进主对话 SSE 流，AGUI 网关映射为
/// CustomEvent（name=<see cref="SubAgentChatStreamContract.EventName"/>）供前端渲染嵌套活动卡.
/// </summary>
public class SubAgentChatEvent
{
    /// <summary>
    /// 事件种类.
    /// </summary>
    public SubAgentChatEventKind Kind { get; init; }

    /// <summary>
    /// 一次派发的追踪 id（同轮多次派发各自独立）.
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// 子智能体 key（具名派发为 team_agent.subs 项 key；通用子代理固定 general）.
    /// </summary>
    public string AgentKey { get; init; } = string.Empty;

    /// <summary>
    /// 子智能体展示名称.
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// 正文增量（Text 事件）.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// 工具名（ToolCall/ToolResult 事件）.
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// 工具结果摘要（ToolResult 事件，截断）.
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// 最终结论（Completed 事件）.
    /// </summary>
    public string FinalText { get; init; } = string.Empty;

    /// <summary>
    /// 执行耗时（毫秒，Completed/Timeout/Failed 时有值）.
    /// </summary>
    public long ElapsedMilliseconds { get; init; }
}
