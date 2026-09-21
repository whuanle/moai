namespace MoAI.Wiki.Services;

/// <summary>
/// 待写入知识库的一条外部内容.
/// </summary>
public class WikiSourceContentItem
{
    /// <summary>
    /// 外部唯一标识（飞书为节点 token，爬虫为归一化页面 URL）.
    /// </summary>
    public string ExternalKey { get; init; } = string.Empty;

    /// <summary>
    /// 外部文档内容标识（飞书为 obj_token，爬虫为页面 URL）.
    /// </summary>
    public string ExternalDocToken { get; init; } = string.Empty;

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 在源中的路径.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// 正文内容（Markdown）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 内容摘要（SHA-256）.
    /// </summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>
    /// 版本标识（飞书为编辑时间）.
    /// </summary>
    public string Revision { get; init; } = string.Empty;
}
