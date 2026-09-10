using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询边详情.
/// </summary>
public class QueryKnowledgeGraphEdgeCommand : IRequest<QueryKnowledgeGraphEdgeCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;
}
