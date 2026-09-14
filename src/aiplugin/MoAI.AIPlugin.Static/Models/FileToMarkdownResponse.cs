using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 文件转 Markdown 响应结果.
/// </summary>
public class FileToMarkdownResponse
{
    /// <summary>
    /// 实际使用的文件名称（传入值或从 Url 自动识别）.
    /// </summary>
    [Description("实际使用的文件名称（传入值或从 Url 自动识别）")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 按扩展名识别出的文件 MIME 类型.
    /// </summary>
    [Description("按扩展名识别出的文件 MIME 类型")]
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// 转换出的 Markdown 内容.
    /// </summary>
    [Description("转换出的 Markdown 内容")]
    public string Markdown { get; set; } = string.Empty;
}
