using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 对话上下文，一个完整的聊天上下文.
/// </summary>
public class AIAssistantChatContext : AIAssistantChatObject
{
    /// <summary>
    /// 历史对话或者上下文信息，创建对话时，如果有提示词，则第一个对话就是提示词.
    /// </summary>
    public virtual IReadOnlyCollection<ChatMessageContent> ChatHistory { get; init; } = new ChatHistory();
}