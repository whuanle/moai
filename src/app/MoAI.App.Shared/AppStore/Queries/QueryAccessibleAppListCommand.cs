using MediatR;
using MoAI.App.Chat.Chats.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AppStore.Queries;

/// <summary>
/// 查询用户可访问的应用列表（公开应用 + 用户加入的团队的应用）.
/// </summary>
public class QueryAccessibleAppListCommand : IUserIdContext, IRequest<QueryAccessibleAppListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 分类 ID（可选，用于筛选指定分类的应用）.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <summary>
    /// 应用名称（可选，用于模糊搜索）.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 限制团队.
    /// </summary>
    public int? TeamId { get; init; }
}
