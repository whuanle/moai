using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// Markdown 转 HTML 请求参数.
/// </summary>
public class MarkdownToHtmlRequest
{
    /// <summary>
    /// 待转换的 Markdown 内容.
    /// </summary>
    [Description("待转换的 Markdown 内容")]
    public string Markdown { get; set; } = string.Empty;
}
