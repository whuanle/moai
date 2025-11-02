namespace MoAI.User.Queries.Responses;

public class QueryUserBindAccountCommandResponse
{
    public IReadOnlyCollection<QueryUserBindAccountCommandResponseItem> Items { get; init; } = Array.Empty<QueryUserBindAccountCommandResponseItem>();
}
