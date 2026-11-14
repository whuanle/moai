namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 导入任务状态常量（kg_import_job.status）.
/// </summary>
public static class KnowledgeGraphImportJobStatus
{
    /// <summary>已入队未开始.</summary>
    public const string Pending = "pending";

    /// <summary>提取文档文本中.</summary>
    public const string Extracting = "extracting";

    /// <summary>模型抽取实体关系中.</summary>
    public const string Parsing = "parsing";

    /// <summary>幂等合并落库中.</summary>
    public const string Merging = "merging";

    /// <summary>完成（result_json 携带统计）.</summary>
    public const string Done = "done";

    /// <summary>失败（error_json 携带原因，可重试）.</summary>
    public const string Failed = "failed";

    /// <summary>已取消（可重试）.</summary>
    public const string Cancelled = "cancelled";
}
