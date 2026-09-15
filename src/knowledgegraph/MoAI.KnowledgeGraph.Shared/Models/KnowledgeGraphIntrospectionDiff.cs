namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 接入图内省相对上次基线的变化.
/// </summary>
/// <param name="AddedLabels">新增节点标签.</param>
/// <param name="RemovedLabels">消失节点标签.</param>
/// <param name="AddedRelationTypes">新增关系类型.</param>
/// <param name="RemovedRelationTypes">消失关系类型.</param>
public sealed record KnowledgeGraphIntrospectionDiff(
    IReadOnlyList<string> AddedLabels,
    IReadOnlyList<string> RemovedLabels,
    IReadOnlyList<string> AddedRelationTypes,
    IReadOnlyList<string> RemovedRelationTypes)
{
    /// <summary>
    /// 是否没有任何变化.
    /// </summary>
    public bool IsEmpty =>
        AddedLabels.Count == 0 &&
        RemovedLabels.Count == 0 &&
        AddedRelationTypes.Count == 0 &&
        RemovedRelationTypes.Count == 0;
}
