namespace MoAI.AI.Models;

/// <summary>
/// Agent 会话快照热态包装（Redis）.
/// </summary>
public sealed class AppAgentSessionSnapshot
{
    /// <summary>
    /// AgentSession 序列化 JSON 文本.
    /// </summary>
    public string Json { get; set; } = string.Empty;
}
