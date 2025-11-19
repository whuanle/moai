using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Classify.Commands;

/// <summary>
/// 修改插件分类.
/// </summary>
public class UpdatePluginClassifyCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string? Description { get; init; } = string.Empty;
}
