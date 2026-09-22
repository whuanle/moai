namespace MoAI.App.Workflow.Nodes;

/// <summary>
/// 知识图谱检索命中项 - 知识图谱检索节点的单条召回结果.
/// </summary>
public class WorkflowGraphSearchHit
{
    /// <summary>
    /// 知识图谱 id.
    /// </summary>
    public long KgId { get; set; }

    /// <summary>
    /// 命中实体（节点）id.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 实体名.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 实体类型名（类型未定义时为空）.
    /// </summary>
    public string? EntityTypeName { get; set; }

    /// <summary>
    /// 实体描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 相似度得分（越大越相似，可能为空）.
    /// </summary>
    public double? Score { get; set; }

    /// <summary>
    /// 该命中含一跳邻居的文本化片段（供 LLM 上下文）.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
