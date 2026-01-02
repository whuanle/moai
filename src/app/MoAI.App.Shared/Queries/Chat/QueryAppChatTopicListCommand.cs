using MediatR;
using MoAI.App.Queries.Chat.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Queries.Chat;

/// <summary>
/// 查询用户在指定应用下的对话列表.
/// </summary>
public class QueryAppChatTopicListCommand : IUserIdContext, IRequest<QueryAppChatTopicListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }
}
