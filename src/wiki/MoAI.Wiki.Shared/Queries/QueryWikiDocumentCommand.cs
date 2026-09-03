using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询文档详情（含正文），仅团队成员可访问.
/// </summary>
public class QueryWikiDocumentCommand : IRequest<QueryWikiDocumentCommandResponse>
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }
}
