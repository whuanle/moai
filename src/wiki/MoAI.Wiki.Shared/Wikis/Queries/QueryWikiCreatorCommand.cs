using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询知识库创建信息，以及判断用户是否有这个知识库的操作权限.
/// </summary>
public class QueryWikiCreatorCommand : IRequest<QueryWikiCreatorCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
