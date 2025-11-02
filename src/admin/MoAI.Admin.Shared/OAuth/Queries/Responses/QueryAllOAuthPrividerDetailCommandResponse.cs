using MoAI.Login.Queries;

namespace MoAI.Admin.OAuth.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryAllOAuthPrividerDetailCommand"/>
/// </summary>
public class QueryAllOAuthPrividerDetailCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<OAuthPrividerDetailItem> Items { get; init; } = Array.Empty<OAuthPrividerDetailItem>();
}
