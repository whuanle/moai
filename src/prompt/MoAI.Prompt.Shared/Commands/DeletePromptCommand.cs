using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 删除提示词.
/// </summary>
public class DeletePromptCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}