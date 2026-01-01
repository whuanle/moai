using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 获取知识库列表.
/// </summary>
public class QueryWikiBaseListCommand : IUserIdContext, IRequest<IReadOnlyCollection<QueryWikiInfoResponse>>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <summary>
    /// 限制查询某个团队下的知识库.
    /// </summary>
    public int? TeamId { get; init; }

    /// <summary>
    /// 只查询私有知识库.
    /// </summary>
    public bool? IsOwn { get; init; }
}
