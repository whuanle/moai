using MediatR;
using MoAI.App.Chat.Works.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Chat.Works.Queries;

/// <summary>
/// 查询用户在指定应用下的对话列表.
/// </summary>
public class QueryChatApptInstanceTopicListCommand : IUserIdContext, IRequest<QueryChatAppInstanceTopicListCommandResponse>
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
