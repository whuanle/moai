using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Classify.Commands;

/// <summary>
/// 删除插件分类.
/// </summary>
public class DeletePluginClassifyCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }
}