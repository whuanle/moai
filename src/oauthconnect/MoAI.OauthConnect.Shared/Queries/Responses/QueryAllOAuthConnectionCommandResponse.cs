namespace MoAI.OauthConnect.Queries.Responses;

/// <summary>
/// QueryAllOAuthConnectionCommandResponse.
/// </summary>
public class QueryAllOAuthConnectionCommandResponse
{
    /// <summary>
    /// 集合.
    /// </summary>
    public IReadOnlyCollection<QueryAllOAuthConnectionCommandResponseItem> Items { get; init; } = new List<QueryAllOAuthConnectionCommandResponseItem>();
}
