namespace MoAI.Plugin.Authorization.Queries.Responses;

/// <summary>
/// 插件授权信息项.
/// </summary>
public class PluginAuthorizationItem
{
    /// <summary>
    /// 插件ID.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; init; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 插件描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 授权的团队列表.
    /// </summary>
    public IReadOnlyCollection<AuthorizedTeamItem> AuthorizedTeams { get; init; } = new List<AuthorizedTeamItem>();
}
