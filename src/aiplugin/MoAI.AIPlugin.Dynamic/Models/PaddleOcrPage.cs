using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PP-OCRv5 单页识别结果.
/// </summary>
public class PaddleOcrPage
{
    /// <summary>
    /// 单页识别文本，由 <c>rec_texts</c> 数组按行用换行拼接.
    /// </summary>
    [Description("单页识别文本（rec_texts 按行拼接，每行一段识别结果）")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 标注识别框的 OCR 结果图（Base64），可能为 <see langword="null"/>（例如 PDF 无可视化结果时）.
    /// </summary>
    [Description("标注识别框的 OCR 结果图（Base64），可视化未启用或 PDF 时可能为 null")]
    public string? OcrImage { get; set; }

    /// <summary>
    /// 输入图像（Base64），可能为 <see langword="null"/>（PDF 时通常为空）.
    /// </summary>
    [Description("输入图像（Base64），PDF 时通常为 null")]
    public string? InputImage { get; set; }
}