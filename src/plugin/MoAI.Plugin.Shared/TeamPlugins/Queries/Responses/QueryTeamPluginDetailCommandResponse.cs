using MoAI.AI.Models;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.Plugin.TeamPlugins.Queries.Responses;

/// <summary>
/// 团队插件详情响应.
/// </summary>
public class QueryTeamPluginDetailCommandResponse : AuditsInfo
{
    /// <summary>
    /// 插件 ID.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 插件描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 插件类型.
    /// </summary>
    public PluginType Type { get; init; }

    /// <summary>
    /// 分类 ID.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 服务器地址.
    /// </summary>
    public string Server { get; init; } = string.Empty;

    /// <summary>
    /// 请求头.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Headers { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// Query 参数.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Queries { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// OpenAPI 文件 ID.
    /// </summary>
    public int OpenapiFileId { get; init; }

    /// <summary>
    /// OpenAPI 文件名称.
    /// </summary>
    public string OpenapiFileName { get; init; } = string.Empty;
}
