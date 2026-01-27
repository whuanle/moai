namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 批量查询节点定义响应.
/// </summary>
public class QueryBatchNodeDefineCommandResponse
{
    /// <summary>
    /// 节点定义列表.
    /// </summary>
    public IReadOnlyList<NodeDefineResponseItem> NodeDefines { get; init; } = Array.Empty<NodeDefineResponseItem>();

    /// <summary>
    /// 失败的请求列表.
    /// </summary>
    public IReadOnlyList<NodeDefineErrorItem> Errors { get; init; } = Array.Empty<NodeDefineErrorItem>();
}

/// <summary>
/// 节点定义响应项.
/// </summary>
public class NodeDefineResponseItem : QueryNodeDefineCommandResponse
{
    /// <summary>
    /// 请求标识符（与请求中的 RequestId 对应）.
    /// </summary>
    public string? RequestId { get; init; }
}

/// <summary>
/// 节点定义错误项.
/// </summary>
public class NodeDefineErrorItem
{
    /// <summary>
    /// 请求标识符.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// 节点类型.
    /// </summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>
    /// 错误消息.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// 错误代码.
    /// </summary>
    public string? ErrorCode { get; init; }
}
