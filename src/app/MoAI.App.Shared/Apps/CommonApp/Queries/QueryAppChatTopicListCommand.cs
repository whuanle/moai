using MediatR;
using MoAI.App.Apps.CommonApp.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Queries;

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
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }
}
