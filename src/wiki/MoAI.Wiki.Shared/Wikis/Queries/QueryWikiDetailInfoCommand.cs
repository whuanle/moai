using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 获取知识库详细信息.
/// </summary>
public class QueryWikiDetailInfoCommand : IRequest<QueryWikiInfoResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }
}
