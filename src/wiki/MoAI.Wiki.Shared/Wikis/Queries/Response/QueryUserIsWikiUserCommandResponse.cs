namespace MoAI.Wiki.Wikis.Queries.Response;

public class QueryUserIsWikiUserCommandResponse
{
    public int WikiId { get; init; }

    public int UserId { get; init; }

    public bool IsWikiUser { get; init; }

    public bool IsWikiRoot { get; init; }
}