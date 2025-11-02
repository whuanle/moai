namespace MoAI.User.Queries.Responses;

public class QueryUserBindAccountCommandResponseItem
{
    public int BindId { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid ProviderId { get; init; }

    public string IconUrl { get; init; } = string.Empty;
}
