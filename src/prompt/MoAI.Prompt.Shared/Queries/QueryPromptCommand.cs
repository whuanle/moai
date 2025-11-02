using MediatR;
using MoAIPrompt.Models;

namespace MoAIPrompt.Queries;

/// <summary>
/// 获取提示词.
/// </summary>
public class QueryPromptCommand : IRequest<PromptItem>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
