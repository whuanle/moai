namespace MoAI.Login.Queries.Responses;

/// <summary>
/// QueryAllOAuthPrividerCommandResponse。
/// </summary>
public class QueryAllOAuthPrividerCommandResponse
{
    /// <summary>
    /// 集合.
    /// </summary>
    public IReadOnlyCollection<QueryAllOAuthPrividerCommandResponseItem> Items { get; init; } = new List<QueryAllOAuthPrividerCommandResponseItem>();
}
