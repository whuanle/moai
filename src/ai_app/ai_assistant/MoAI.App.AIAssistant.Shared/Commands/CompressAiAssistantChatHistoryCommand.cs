using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 压缩 AI 助手聊天历史记录.
/// </summary>
public class CompressAiAssistantChatHistoryCommand : IUserIdContext, IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 聊天 ID.
    /// </summary>
    public required Guid ChatId { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
