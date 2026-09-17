namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库上传文件大小上限响应.
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
}
