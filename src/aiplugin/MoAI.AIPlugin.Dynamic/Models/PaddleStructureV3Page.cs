using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PP-StructureV3 单页版面解析结果.
/// </summary>
public class PaddleStructureV3Page
{
    /// <summary>
    /// 简化版预测结果原文（JSON 文本），由 <c>prunedResult</c> 原样回传，便于上层自行解析.
    /// </summary>
    [Description("简化版预测结果原文 JSON 文本（prunedResult 原样回传）；字段形态按 PaddleOCR 官方文档定义")]
    public string? PrunedResultJson { get; set; }

    /// <summary>
    /// 输出图像（按图像名称索引的 Base64 字典）.
    /// </summary>
    [Description("输出图像字典：键为图像名称，值为 Base64；包含可视化结果与版面区域截图")]
    public IReadOnlyDictionary<string, string>? OutputImages { get; init; }

    /// <summary>
    /// 输入图像（Base64），PDF 时可能为 <see langword="null"/>.
    /// </summary>
    [Description("输入图像（Base64），PDF 时通常为 null")]
    public string? InputImage { get; set; }

    /// <summary>
    /// 印章识别子产线产出的文本（仅在启用 <c>useSealRecognition=true</c> 时填充）.
    /// </summary>
    [Description("印章识别结果文本列表；从 seal_res_list 逐条提取 rec_texts")]
    public IReadOnlyList<string> SealTexts { get; init; } = [];
}