using MediatR;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 使用 AI 优化提示词.
/// </summary>
public class AiOptimizePromptCommand : IRequest<QueryAiOptimizePromptCommandResponse>
{
    /// <summary>
    /// 当前用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// AI 模型 id.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 用户原本的提示词
    /// </summary>
    public string SourcePrompt { get; init; } = string.Empty;
}
