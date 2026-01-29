namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 查询 AI 对话节点定义响应.
/// </summary>
public class QueryAiChatNodeDefineCommandResponse
{
    /// <summary>
    /// AI 对话节点定义列表.
    /// </summary>
    public IReadOnlyList<QueryNodeDefineCommandResponse> Nodes { get; init; } = Array.Empty<QueryNodeDefineCommandResponse>();
}
