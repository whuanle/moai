namespace MoAI.Wiki.DocumentEmbedding.Models;

public class QueryWikiDocumentChunksCommandResponse
{
    /// <summary>
    /// 分块的内容.
    /// </summary>
    public IReadOnlyCollection<WikiDocumenChunkItem> Items { get; init; } = Array.Empty<WikiDocumenChunkItem>();
}