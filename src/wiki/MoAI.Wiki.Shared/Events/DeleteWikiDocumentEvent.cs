using MediatR;

namespace MoAI.Wiki.Events;

/// <summary>
/// 通知删除了文档.
/// </summary>
public class DeleteWikiDocumentEvent : INotification
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }
}