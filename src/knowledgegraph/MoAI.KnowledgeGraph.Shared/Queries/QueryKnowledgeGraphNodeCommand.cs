using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询节点详情.
/// </summary>
public class QueryKnowledgeGraphNodeCommand : IRequest<QueryKnowledgeGraphNodeCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;
}
