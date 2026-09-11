using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// Markdown 转 HTML 响应结果.
/// </summary>
public class MarkdownToHtmlResponse
{
    /// <summary>
    /// 转换后的 HTML 内容.
    /// </summary>
    [Description("转换后的 HTML 内容")]
    public string Html { get; set; } = string.Empty;
}
