namespace MoAI.AI;

/// <summary>
/// Agent 运行时常量.
/// </summary>
public static class AppAgentConstants
{
    /// <summary>
    /// 对外暴露的 AG-UI 动态派发 Agent 名称，同时作为 AgentSessionStore 的 keyed 服务名.
    /// </summary>
    public const string AgentName = "moai-app-agent";

    /// <summary>
    /// 会话 id 在 AgentSession.StateBag 中的键.
    /// </summary>
    public const string SessionIdStateKey = "moai:app:sessionId";

    /// <summary>
    /// 默认会话标题.
    /// </summary>
    public const string DefaultSessionTitle = "未命名标题";

    /// <summary>
    /// 会话解析失败时附加在提示文本前的标记，供前端识别并重建调试会话.
    /// </summary>
    public const string SessionResolveErrorMarker = "[[moai:session-not-found]]";
}
