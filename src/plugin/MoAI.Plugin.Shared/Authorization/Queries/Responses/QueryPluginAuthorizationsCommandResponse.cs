namespace MoAI.Plugin.Authorization.Queries.Responses;

/// <summary>
/// 插件授权列表响应.
/// </summary>
public class QueryPluginAuthorizationsCommandResponse
{
    /// <summary>
    /// 插件授权列表.
    /// </summary>
    public IReadOnlyCollection<PluginAuthorizationItem> Plugins { get; init; } = new List<PluginAuthorizationItem>();
}
