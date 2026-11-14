namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库上传约束响应.
/// </summary>
public class QueryWikiUploadLimitCommandResponse
{
    /// <summary>
    /// 上限大小（MB），0 表示不限制（仅受平台硬上限 1GB 约束）.
    /// </summary>
    public int MaxFileSizeMb { get; set; }

    /// <summary>
    /// 上限大小（字节），0 表示不限制.
    /// </summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>
    /// 允许上传的文件类型（小写扩展名，含点号），与系统设置「知识库允许上传的文件类型」一致.
    /// </summary>
    public List<string> AllowedFileTypes { get; set; } = new();
}
