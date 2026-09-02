using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询知识库详情，仅团队成员可访问.
/// </summary>
public class QueryWikiCommand : IRequest<QueryWikiCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }
}
