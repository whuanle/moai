namespace MoAI.Plugin.Authorization.Queries.Responses;

/// <summary>
/// 团队授权信息项.
/// </summary>
public class TeamAuthorizationItem
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string TeamName { get; init; } = default!;

    /// <summary>
    /// 授权的插件列表.
    /// </summary>
    public IReadOnlyCollection<AuthorizedPluginItem> AuthorizedPlugins { get; init; } = new List<AuthorizedPluginItem>();
}
