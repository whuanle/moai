namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 导入任务列表项.
/// </summary>
public class KnowledgeGraphImportJobItem
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public long JobId { get; init; }

    /// <summary>
    /// 任务类型：file=AI 文档导入.
    /// </summary>
    public string Type { get; init; } = "file";

    /// <summary>
    /// 导入文件名.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 任务状态：pending/extracting/parsing/merging/done/failed/cancelled.
    /// </summary>
    public string Status { get; init; } = "pending";

    /// <summary>
    /// 总行数（解析后回填，解析前为 0）.
    /// </summary>
    public int TotalRows { get; init; }

    /// <summary>
    /// 已完成行数.
    /// </summary>
    public int DoneRows { get; init; }

    /// <summary>
    /// 失败原因（成功为 null）.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 结果统计（未完成为 null）.
    /// </summary>
    public ImportKnowledgeGraphFromFileResponse? Result { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}

/// <summary>
/// 导入任务列表响应.
/// </summary>
public class QueryKnowledgeGraphImportJobsCommandResponse
{
    /// <summary>
    /// 任务列表（按 id 倒序）.
    /// </summary>
    public List<KnowledgeGraphImportJobItem> Items { get; init; } = [];

    /// <summary>
    /// 总数.
    /// </summary>
    public long Total { get; init; }
}
