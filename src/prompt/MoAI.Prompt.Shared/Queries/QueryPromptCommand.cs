using MediatR;
using MoAI.Infra.Models;
using MoAIPrompt.Models;

namespace MoAIPrompt.Queries;

/// <summary>
/// 获取提示词.
/// </summary>
public class QueryPromptCommand : IRequest<PromptItem>, IUserIdContext
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
