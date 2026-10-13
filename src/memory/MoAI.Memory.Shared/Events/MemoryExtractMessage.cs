using System;
using Maomi.MQ;

namespace MoAI.Memory.Events;

/// <summary>
/// 会话记忆提取任务消息（会话落库 flush 后投递，仅携带会话 id，消费端读库处理）.
/// </summary>
[RouterKey("app.memory.extract")]
public class MemoryExtractMessage
{
    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; init; }
}
