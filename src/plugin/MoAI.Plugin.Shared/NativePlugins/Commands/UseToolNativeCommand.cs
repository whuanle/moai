using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.NativePlugins.Commands;

/// <summary>
/// 使用这个工具插件，会添加到数据库.
/// </summary>
public class UseToolNativeCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 插件 key.
    /// </summary>
    public string TemplatePluginKey { get; set; } = string.Empty;

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;
}
