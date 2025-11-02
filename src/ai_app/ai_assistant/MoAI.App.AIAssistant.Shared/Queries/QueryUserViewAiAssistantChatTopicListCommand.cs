using MediatR;
using MoAI.App.AIAssistant.Queries.Responses;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// 查询用户的 AI 助手对话主题列表.
/// </summary>
public class QueryUserViewAiAssistantChatTopicListCommand : IRequest<QueryAiAssistantChatTopicListCommandResponse>
{
}
