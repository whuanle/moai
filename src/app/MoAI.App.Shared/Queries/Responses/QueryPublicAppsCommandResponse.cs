namespace MoAI.App.Queries.Responses;

/// <summary>
/// 平台公开应用列表响应.
/// </summary>
public class QueryPublicAppsCommandResponse
{
    /// <summary>
    /// 应用集合.
    /// </summary>
    public IReadOnlyList<AppItem> Items { get; set; } = new List<AppItem>();
}
