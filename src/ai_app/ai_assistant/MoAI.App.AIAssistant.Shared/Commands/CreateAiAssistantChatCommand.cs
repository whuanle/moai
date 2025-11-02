using MediatR;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.App.AIAssistant.Models;

namespace MoAIChat.Core.Handlers;

/// <summary>
/// 创建新的对话，要使用 ProcessingAiAssistantChatCommand 发起对话，此接口只用于新建第一条记录.
/// </summary>
public class CreateAiAssistantChatCommand : AIAssistantChatObject, IRequest<CreateAiAssistantChatCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
