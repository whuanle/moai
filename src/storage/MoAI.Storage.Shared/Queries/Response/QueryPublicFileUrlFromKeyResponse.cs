namespace MoAI.Store.Queries.Response;

/// <summary>
/// 获取文件的访问路径，只支持公有文件.
/// </summary>
public class QueryPublicFileUrlFromKeyResponse
{
    public IReadOnlyDictionary<string, Uri> Urls { get; init; } = new Dictionary<string, Uri>();
}