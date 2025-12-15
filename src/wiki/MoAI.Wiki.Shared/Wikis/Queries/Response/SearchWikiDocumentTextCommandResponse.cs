namespace MoAI.Wiki.Wikis.Queries.Response;

public class SearchWikiDocumentTextCommandResponse
{
    /// <summary>
    /// 提问.
    /// </summary>
    public string Query { get; init; }

    /// <summary>
    /// Ai 的回答.
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>
    /// 搜索结果.
    /// </summary>
    public IReadOnlyCollection<SearchWikiDocumentTextItem> SearchResult { get; init; } = Array.Empty<SearchWikiDocumentTextItem>();
}
