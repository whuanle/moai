using MediatR;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 更新 AI 对话参数.
/// </summary>
public class UpdateAiAssistanChatConfigCommand : AIAssistantChatObject, IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// AI 的头像.
    /// </summary>
    public string AiAvatar { get; init; } = string.Empty;
}
