using MediatR;
using MoAI.Database;

namespace MoAI.Wiki.Events;

/// <summary>
/// <inheritdoc cref="DeleteWikiDocumentEvent"/>
/// </summary>
public class DeleteWikiDocumentEventHandler : INotificationHandler<DeleteWikiDocumentEvent>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentEventHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteWikiDocumentEventHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task Handle(DeleteWikiDocumentEvent notification, CancellationToken cancellationToken)
    {
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocuments.Where(x => x.Id == notification.DocumentId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginCrawlerPages.Where(a => a.WikiDocumentId == notification.DocumentId));

        // 清空向量
    }
}
