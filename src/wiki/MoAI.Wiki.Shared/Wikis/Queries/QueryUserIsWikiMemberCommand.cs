using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 用户是否知识库成员.
/// </summary>
public class QueryUserIsWikiMemberCommand : IRequest<QueryUserIsWikiMemberCommandResponse>, IUserIdContext
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
