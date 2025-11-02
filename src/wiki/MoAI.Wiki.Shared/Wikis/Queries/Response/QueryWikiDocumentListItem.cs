using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Queries.Response;

public class QueryWikiDocumentListItem : AuditsInfo
{
    public int DocumentId { get; init; }

    public required string FileName { get; init; }

    public int FileSize { get; init; }

    public required string ContentType { get; init; }

    /// <summary>
    /// 是否有向量化内容.
    /// </summary>
    public bool Embedding { get; init; }
}