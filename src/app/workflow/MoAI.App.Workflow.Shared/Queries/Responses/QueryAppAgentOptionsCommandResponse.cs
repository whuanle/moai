namespace MoAI.App.Workflow.Queries.Responses;

/// <summary>
/// Agent 应用可选项（流程设计器 agentApp 节点）.
/// </summary>
public class QueryAppAgentOptionsCommandResponse
{
    /// <summary>
    /// 可选项列表.
    /// </summary>
    public IReadOnlyList<AppAgentOptionItem> Items { get; init; } = [];
}

/// <summary>
/// Agent 应用可选项.
/// </summary>
public class AppAgentOptionItem
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 头像 objectKey.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>
    /// 引入该应用是否与当前流程构成循环嵌套（true 时前端禁选）.
    /// </summary>
    public bool Circular { get; init; }
}
