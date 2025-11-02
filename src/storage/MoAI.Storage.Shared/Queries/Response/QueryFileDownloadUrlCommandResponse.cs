namespace MoAI.Storage.Queries.Response;

/// <summary>
/// 查询文件下载地址响应.
/// </summary>
public class QueryFileDownloadUrlCommandResponse
{
    /// <summary>
    /// 地址.
    /// </summary>
    public IReadOnlyDictionary<string, Uri> Urls { get; init; } = new Dictionary<string, Uri>();
}
