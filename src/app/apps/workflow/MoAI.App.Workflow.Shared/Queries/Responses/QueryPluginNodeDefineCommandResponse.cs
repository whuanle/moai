namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 查询插件节点定义响应.
/// </summary>
public class QueryPluginNodeDefineCommandResponse
{
    /// <summary>
    /// 插件节点定义列表.
    /// </summary>
    public IReadOnlyList<QueryNodeDefineCommandResponse> Nodes { get; init; } = Array.Empty<QueryNodeDefineCommandResponse>();
}
