using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 文本提取响应结果.
/// </summary>
public class TextExtractResponse
{
    /// <summary>
    /// 提取出的文本内容.
    /// </summary>
    [Description("提取出的文本内容")]
    public string Text { get; set; } = string.Empty;
}
