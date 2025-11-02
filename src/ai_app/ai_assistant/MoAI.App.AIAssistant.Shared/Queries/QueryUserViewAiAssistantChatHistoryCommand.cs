using MediatR;
using MoAI.App.AIAssistant.Queries.Responses;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// 查询对话记录.
/// </summary>
public class QueryUserViewAiAssistantChatHistoryCommand : IRequest<QueryAiAssistantChatHistoryCommandResponse>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;

    /// <summary>
    /// 不包含历史记录，只查基础信息.
    /// </summary>
    public bool IsBaseInfo { get; init; } = default!;
}
