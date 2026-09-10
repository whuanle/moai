using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询知识图谱详情.
/// </summary>
public class QueryKnowledgeGraphCommand : IRequest<QueryKnowledgeGraphCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }
}
