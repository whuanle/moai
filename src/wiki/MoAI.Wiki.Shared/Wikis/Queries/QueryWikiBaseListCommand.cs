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
    /// 查询类型.
    /// </summary>
    public WikiQueryType QueryType { get; init; }
}
