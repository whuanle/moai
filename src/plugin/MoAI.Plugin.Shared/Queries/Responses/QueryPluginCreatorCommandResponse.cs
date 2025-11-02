namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// 查询插件创建信息.
/// </summary>
public class QueryPluginCreatorCommandResponse
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;

    /// <summary>
    /// 是否存在.
    /// </summary>
    public bool IsExist { get; init; }

    /// <summary>
    /// 是否系统插件.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 创建这 id.
    /// </summary>
    public int CreatorId { get; init; }
}