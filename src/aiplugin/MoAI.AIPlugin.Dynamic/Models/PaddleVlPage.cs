using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PaddleOCR-VL 单页视觉语言模型解析结果.
/// </summary>
public class PaddleVlPage
{
    /// <summary>
    /// 单页 Markdown 文本（视觉语言模型直接产出）.
    /// </summary>
    [Description("单页 Markdown 文本；由 PaddleOCR-VL 直接产出，可直接渲染为 Markdown")]
    public string? MarkdownText { get; set; }

    /// <summary>
    /// Markdown 内嵌的图片字典：键为 Markdown 中的相对路径，值为 Base64.
    /// </summary>
    [Description("Markdown 内嵌图片字典：键为 Markdown 中的相对路径，值为 Base64")]
    public IReadOnlyDictionary<string, string>? MarkdownImages { get; init; }

    /// <summary>
    /// 输入图像（Base64），PDF 时可能为 <see langword="null"/>.
    /// </summary>
    [Description("输入图像（Base64），PDF 时通常为 null")]
    public string? InputImage { get; set; }

    /// <summary>
    /// 简化版预测结果原文（JSON 文本），由 <c>prunedResult</c> 原样回传.
    /// </summary>
    [Description("简化版预测结果原文 JSON 文本（prunedResult 原样回传）；可与 Markdown 配合做精细还原")]
    public string? PrunedResultJson { get; set; }
}