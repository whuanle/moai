using System;

namespace MoAI.Memory.Services;

/// <summary>
/// Agent 长期记忆向量记录（集合 __agent_mem_{appId}，一行一条记忆；时间以 Unix 毫秒存储以匹配向量库简单类型映射）.
/// </summary>
public sealed class AgentMemoryVectorRecord
{
    /// <summary>
    /// 记忆 id（主键）.
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// 用户 id（隔离维度，索引列）.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 记忆类型：0 语义 / 1 情景 / 2 程序（索引列）.
    /// </summary>
    public int MemoryType { get; set; }

    /// <summary>
    /// 记忆内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 来源会话 id.
    /// </summary>
    public Guid SourceSessionId { get; set; }

    /// <summary>
    /// 创建时间（Unix 毫秒）.
    /// </summary>
    public long CreateTimeMs { get; set; }

    /// <summary>
    /// 最近更新时间（Unix 毫秒）.
    /// </summary>
    public long UpdateTimeMs { get; set; }

    /// <summary>
    /// 内容向量.
    /// </summary>
    public ReadOnlyMemory<float> Embedding { get; set; }
}
