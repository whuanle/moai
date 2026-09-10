using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询团队下的知识图谱列表.
/// </summary>
public class QueryKnowledgeGraphsCommand : IRequest<QueryKnowledgeGraphsCommandResponse>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }
}
