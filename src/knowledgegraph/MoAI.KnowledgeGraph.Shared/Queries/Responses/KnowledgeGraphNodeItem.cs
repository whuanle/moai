namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 节点列表项.
/// </summary>
public class KnowledgeGraphNodeItem
{
    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型 id（托管图；接入图为 0）.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 实体类型名（接入图为节点首个标签；托管图为空）.
    /// </summary>
    public string? EntityLabel { get; init; }

    /// <summary>
    /// 实例属性值（键为实体类型定义的属性名）.
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = new();

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
