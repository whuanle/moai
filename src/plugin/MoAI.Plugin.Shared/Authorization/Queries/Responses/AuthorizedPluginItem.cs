namespace MoAI.Plugin.Authorization.Queries.Responses;

/// <summary>
/// 授权插件信息项.
/// </summary>
public class AuthorizedPluginItem
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
    /// 授权ID.
    /// </summary>
    public int AuthorizationId { get; init; }
}
