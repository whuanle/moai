namespace MoAI.Workflow.Models;

/// <summary>
/// 工作流节点连接模型，表示两个节点之间的连接关系.
/// </summary>
public class Connection
{
    /// <summary>
    /// 源节点的唯一标识符.
    /// </summary>
    public string SourceNodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 目标节点的唯一标识符.
    /// </summary>
    public string TargetNodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 连接标签，可选，用于条件分支标签（如 "true", "false"）.
    /// </summary>
    public string? Label { get; set; }
}
