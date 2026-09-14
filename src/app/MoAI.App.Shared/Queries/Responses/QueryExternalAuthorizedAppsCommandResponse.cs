namespace MoAI.App.Queries.Responses;

/// <summary>
/// 外部 token 授权范围内的应用列表.
/// </summary>
public class QueryExternalAuthorizedAppsCommandResponse
{
    /// <summary>
    /// 应用列表.
    /// </summary>
    public IReadOnlyList<ExternalAppItem> Items { get; init; } = new List<ExternalAppItem>();
}
