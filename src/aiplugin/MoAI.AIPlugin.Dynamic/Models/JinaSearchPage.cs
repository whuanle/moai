using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Jina 搜索单条结果.
/// </summary>
public class JinaSearchPage
{
    /// <summary>
    /// 页面标题.
    /// </summary>
    [Description("页面标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 页面地址.
    /// </summary>
    [Description("页面地址")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 页面摘要描述.
    /// </summary>
    [Description("页面摘要描述")]
    public string? Description { get; set; }

    /// <summary>
    /// 页面正文.
    /// </summary>
    [Description("页面正文（格式由请求 ReturnFormat 决定，默认 Markdown）")]
    public string? Content { get; set; }
}
