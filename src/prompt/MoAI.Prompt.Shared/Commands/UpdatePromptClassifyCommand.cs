using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 修改提示词分类.
/// </summary>
public class UpdatePromptClassifyCommand : IRequest<EmptyCommandResponse>
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
