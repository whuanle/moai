using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 进行对话，对话时，History 每次做增量传递.
/// </summary>
public class ProcessingAiAssistantChatCommand : IStreamRequest<AiProcessingChatItem>, IUserIdContext
{
    /// <summary>
    /// 对话 id，id 为空时自动新建.
    /// </summary>
    public Guid ChatId { get; init; } = default!;

    /// <summary>
    /// 用户的提问.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <inheritdoc/>
    public int ContextUserId { get; init; }
}
