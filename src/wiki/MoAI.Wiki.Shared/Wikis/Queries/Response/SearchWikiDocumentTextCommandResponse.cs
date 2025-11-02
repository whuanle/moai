using Microsoft.KernelMemory;

namespace MoAI.Wiki.Wikis.Queries.Response;

public class SearchWikiDocumentTextCommandResponse
{
    public required SearchResult SearchResult { get; init; }
}