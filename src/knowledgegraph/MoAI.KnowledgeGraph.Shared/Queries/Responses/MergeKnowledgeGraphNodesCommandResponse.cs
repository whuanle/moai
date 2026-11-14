namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 实体合并结果.
/// </summary>
public class MergeKnowledgeGraphNodesCommandResponse
{
    /// <summary>
    /// 重挂到目标节点的新建边数量（同三元组已存在跳过、自环丢弃不计）.
    /// </summary>
    public int RehungEdges { get; init; }

    /// <summary>
    /// 因合并成为自环（两端均为被合并节点）而丢弃的边数量.
    /// </summary>
    public int DroppedSelfLoopEdges { get; init; }

    /// <summary>
    /// 被删除的源节点数量.
    /// </summary>
    public int RemovedNodes { get; init; }
}
