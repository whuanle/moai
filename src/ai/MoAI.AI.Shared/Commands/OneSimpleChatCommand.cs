using MediatR;
using MoAI.AI.Models;

namespace MoAI.AI.Commands;

/// <summary>
/// 一次简单的提问.
/// </summary>
public class OneSimpleChatCommand : IRequest<OneSimpleChatCommandResponse>
{
    /// <summary>
    /// 对话 AI 信息.
    /// </summary>
    public AiEndpoint Endpoint { get; init; } = default!;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string Prompt { get; init; } = default!;

    /// <summary>
    /// 问题.
    /// </summary>
    public string Question { get; init; } = default!;
}
