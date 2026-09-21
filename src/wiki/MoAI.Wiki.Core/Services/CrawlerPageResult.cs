namespace MoAI.Wiki.Services;

/// <summary>
/// 爬虫单页抓取结果.
/// </summary>
public class CrawlerPageResult
{
    /// <summary>
    /// 页面地址.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// 页面标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 知识库文档 id（未落库时为 0）.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 处理结果：created / updated / unchanged / skipped / failed.
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// 结果说明.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
