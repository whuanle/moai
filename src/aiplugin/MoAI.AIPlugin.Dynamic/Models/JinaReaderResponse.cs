using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Jina 网页阅读响应结果.
/// </summary>
public class JinaReaderResponse
{
    /// <summary>
    /// 实际阅读的页面地址（重定向后）.
    /// </summary>
    [Description("实际阅读的页面地址（重定向后）")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 页面标题.
    /// </summary>
    [Description("页面标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 页面摘要描述.
    /// </summary>
    [Description("页面摘要描述")]
    public string? Description { get; set; }

    /// <summary>
    /// 页面正文.
    /// </summary>
    [Description("页面正文（格式由请求 ReturnFormat 决定，默认 Markdown）")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 页面发布时间.
    /// </summary>
    [Description("页面发布时间（页面可识别时返回）")]
    public string? PublishTime { get; set; }

    /// <summary>
    /// 链接摘要.
    /// </summary>
    [Description("链接摘要（WithLinksSummary 开启时返回）：地址 → 锚文本")]
    public IReadOnlyDictionary<string, string>? Links { get; init; }

    /// <summary>
    /// 图片摘要.
    /// </summary>
    [Description("图片摘要（WithImagesSummary 开启时返回）：地址 → 说明")]
    public IReadOnlyDictionary<string, string>? Images { get; init; }

    /// <summary>
    /// 本次转换消耗的 token 数.
    /// </summary>
    [Description("本次转换消耗的 token 数")]
    public int? UsageTokens { get; set; }
}
