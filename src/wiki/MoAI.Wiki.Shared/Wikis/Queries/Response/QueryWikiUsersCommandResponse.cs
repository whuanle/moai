namespace MoAI.Wiki.Wikis.Queries.Response;

public class QueryWikiUsersCommandResponse
{
    public IReadOnlyCollection<QueryWikiUsersCommandResponseItem> Users { get; init; } = Array.Empty<QueryWikiUsersCommandResponseItem>();
}
