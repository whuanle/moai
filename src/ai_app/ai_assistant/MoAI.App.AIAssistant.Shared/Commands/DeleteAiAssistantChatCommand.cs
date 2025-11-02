using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 删除对话记录.
/// </summary>
public class DeleteAiAssistantChatCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;
}