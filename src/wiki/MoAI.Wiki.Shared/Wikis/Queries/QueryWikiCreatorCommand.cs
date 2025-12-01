using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询知识库创建信息.
/// </summary>
public class QueryWikiCreatorCommand : IRequest<QueryWikiCreatorCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int WikiId { get; init; } = default!;
}
