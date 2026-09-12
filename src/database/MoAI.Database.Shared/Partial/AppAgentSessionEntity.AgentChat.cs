namespace MoAI.Database.Entities;

/// <summary>
/// Agent 应用会话实体的冷快照扩展列。
/// <para>放在 <c>Partial/</c> 目录而非 <c>Entities/</c>，避免 PostgresScaffold 分发时（先删后拷）被覆盖。</para>
/// </summary>
public partial class AppAgentSessionEntity
{
    /// <summary>
    /// Agent 会话冷快照（AgentSession 序列化 JSON，含上下文压缩索引）；Redis 热态失效后据此恢复，null=无.
    /// </summary>
    public string? State { get; set; }
}
