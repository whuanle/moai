using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 文本提取请求参数.
/// </summary>
public class TextExtractRequest
{
    /// <summary>
    /// 文件名称（需带扩展名，用于识别格式）.
    /// </summary>
    [Description("文件名称（需带扩展名，如 test.pdf/test.docx/test.html）")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件地址（http/https 下载地址）.
    /// </summary>
    [Description("文件地址（http/https 下载地址）")]
    public string Url { get; set; } = string.Empty;
}
