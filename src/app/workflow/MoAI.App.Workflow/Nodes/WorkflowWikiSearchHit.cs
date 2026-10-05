using System.Collections.Generic;

namespace MoAI.App.Workflow.Nodes;

/// <summary>
/// 知识库检索命中项 - 知识库检索节点的单条召回结果.
/// </summary>
public class WorkflowWikiSearchHit
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文档名称.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// 切片 id.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 切片在文档中的序号（从 0 开始）.
    /// </summary>
    public int? ChunkIndex { get; set; }

    /// <summary>
    /// 文档切片总数.
    /// </summary>
    public int? DocumentChunkCount { get; set; }

    /// <summary>
    /// 切片内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度得分（越大越相似，可能为空）.
    /// </summary>
    public double? Score { get; set; }

    /// <summary>
    /// 重排序得分（知识库配置了重排序模型且重排成功时才有值）.
    /// </summary>
    public double? RerankScore { get; set; }

    /// <summary>
    /// 相邻上下文片段（命中片段的前一个与后一个，已按全文去重）.
    /// </summary>
    public List<WorkflowWikiSearchContextChunk> Context { get; set; } = new();
}

/// <summary>
/// 知识库检索命中片段的相邻上下文片段.
/// </summary>
public class WorkflowWikiSearchContextChunk
{
    /// <summary>
    /// 切片在文档中的序号（从 0 开始）.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 切片内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
