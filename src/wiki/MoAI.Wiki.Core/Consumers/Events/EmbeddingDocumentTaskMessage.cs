using System;
using Maomi.MQ;

namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// 知识库文档向量化任务消息.
/// </summary>
[RouterKey("wiki.document.embedding")]
public class EmbeddingDocumentTaskMessage
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }
}
