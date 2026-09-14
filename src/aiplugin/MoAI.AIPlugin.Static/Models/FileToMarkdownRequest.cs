using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 文件转 Markdown 请求参数.
/// </summary>
public class FileToMarkdownRequest
{
    /// <summary>
    /// 文件地址（http/https 下载地址）.
    /// </summary>
    [Description("文件地址（http/https 下载地址）")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 可选：文件名称（需带扩展名）；留空时从 Url 路径末段自动识别.
    /// </summary>
    [Description("可选：文件名称（需带扩展名，如 report.pdf）；留空时从 Url 路径末段自动识别")]
    public string FileName { get; set; } = string.Empty;
}
