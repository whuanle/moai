using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 删除提示词.
/// </summary>
public class DeletePromptCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }
}