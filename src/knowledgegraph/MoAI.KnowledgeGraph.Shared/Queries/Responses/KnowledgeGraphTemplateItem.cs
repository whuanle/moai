namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 模板列表项.
/// </summary>
public class KnowledgeGraphTemplateItem
{
    /// <summary>
    /// 模板 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型名称.
    /// </summary>
    public List<string> EntityTypes { get; init; } = new();

    /// <summary>
    /// 关系类型名称.
    /// </summary>
    public List<string> RelationTypes { get; init; } = new();
}
