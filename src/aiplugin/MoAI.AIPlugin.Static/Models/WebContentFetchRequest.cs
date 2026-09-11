using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 网页内容抓取请求参数.
/// </summary>
public class WebContentFetchRequest
{
    /// <summary>
    /// 目标网页链接.
    /// </summary>
    [Description("目标网页链接")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 是否提取 HTML 中的纯文本内容；为 false 时返回原始 HTML.
    /// </summary>
    [Description("是否提取 HTML 中的纯文本内容；为 false 时返回原始 HTML")]
    public bool ExtractText { get; set; } = true;
}
