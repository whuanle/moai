using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询图谱 schema（实体类型 + 关系类型）.
/// </summary>
public class QueryKnowledgeGraphSchemaCommand : IRequest<QueryKnowledgeGraphSchemaCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }
}
