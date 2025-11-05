using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.BuiltCommands;

/// <summary>
/// 创建内置插件实例.
/// </summary>
public class CreateInternalPluginCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 内置插件模板 key.
    /// </summary>
    public string TemplatePluginKey { get; init; } = string.Empty;

    /// <summary>
    /// 插件名称，只能纯字母，用于给 AI 使用.
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
    /// 参数.
    /// </summary>
    public string Config { get; init; } = string.Empty!;
}