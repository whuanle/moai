using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Commands;

/// <summary>
/// 更新 openapi 文件，支持 json、yaml.
/// </summary>
public class UpdateOpenApiPluginCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 上传的 id.
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 插件标题，可中文.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;

    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// MCP Service 地址.
    /// </summary>
    public Uri ServerUrl { get; init; } = default!;

    /// <summary>
    /// Header 头部信息.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Header { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// Query 字典.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Query { get; init; } = Array.Empty<KeyValueString>();
}
