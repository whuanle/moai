using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 更新提示词.
/// </summary>
public class UpdatePromptCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 新的分类id.
    /// </summary>
    public int PromptClassId { get; init; }

    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 助手设定,markdown.
    /// </summary>
    public string Content { get; init; } = default!;

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }
}
