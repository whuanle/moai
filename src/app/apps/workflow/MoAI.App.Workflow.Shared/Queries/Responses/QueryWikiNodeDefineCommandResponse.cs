namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 查询知识库节点定义响应.
/// </summary>
public class QueryWikiNodeDefineCommandResponse
{
    /// <summary>
    /// 知识库节点定义列表.
    /// </summary>
    public IReadOnlyList<QueryNodeDefineCommandResponse> Nodes { get; init; } = Array.Empty<QueryNodeDefineCommandResponse>();
}
