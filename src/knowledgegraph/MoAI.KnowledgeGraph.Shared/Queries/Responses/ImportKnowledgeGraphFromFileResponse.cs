namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// AI 导入文件生成图谱的结果统计.
/// </summary>
public class ImportKnowledgeGraphFromFileResponse
{
    /// <summary>
    /// 新增实体（节点）数.
    /// </summary>
    public int NodesCreated { get; init; }

    /// <summary>
    /// 新增关系（边）数.
    /// </summary>
    public int EdgesCreated { get; init; }

    /// <summary>
    /// 跳过的实体数（类型不存在/名称为空/重复合并）.
    /// </summary>
    public int SkippedNodes { get; init; }

    /// <summary>
    /// 跳过的关系数（类型不存在/端点缺失/约束不满足）.
    /// </summary>
    public int SkippedEdges { get; init; }

    /// <summary>
    /// 提取的文本长度（字符）.
    /// </summary>
    public int ContentLength { get; init; }

    /// <summary>
    /// 提取文本是否因超上限被截断.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// 附加说明（如 AI 未返回可解析内容时的提示）.
    /// </summary>
    public string? Message { get; init; }
}
