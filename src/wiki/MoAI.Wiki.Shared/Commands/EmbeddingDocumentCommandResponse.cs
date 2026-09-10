namespace MoAI.Wiki.Commands;

/// <summary>
/// 触发知识库文档向量化响应.
/// </summary>
public class EmbeddingDocumentCommandResponse
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }
}