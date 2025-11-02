namespace MoAIDocument.Core.Consumers.Events;

/// <summary>
/// 发送消息，后台执行任务.
/// </summary>
public class EmbeddingDocumentTaskMesage
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }
}
