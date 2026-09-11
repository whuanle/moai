using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 网页内容抓取响应结果.
/// </summary>
public class WebContentFetchResponse
{
    /// <summary>
    /// 网页内容（ExtractText 为 true 时为纯文本，否则为原始 HTML）.
    /// </summary>
    [Description("网页内容（ExtractText 为 true 时为纯文本，否则为原始 HTML）")]
    public string Content { get; set; } = string.Empty;
}
