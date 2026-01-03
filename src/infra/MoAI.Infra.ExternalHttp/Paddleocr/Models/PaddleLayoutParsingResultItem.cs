using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// 单个文档解析结果项.
/// </summary>
public class PaddleLayoutParsingResultItem
{
    /// <summary>
    /// 简化版的预测结果.
    /// </summary>
    [JsonPropertyName("prunedResult")]
    public object? PrunedResult { get; set; }

    /// <summary>
    /// Markdown结果.
    /// </summary>
    [JsonPropertyName("markdown")]
    public PaddleLayoutParsingMarkdown? Markdown { get; set; }

    /// <summary>
    /// 输出图像 (Base64). 键为图像名称.
    /// </summary>
    [JsonPropertyName("outputImages")]
    public Dictionary<string, string>? OutputImages { get; set; }

    /// <summary>
    /// 输入图像 (Base64).
    /// </summary>
    [JsonPropertyName("inputImage")]
    public string? InputImage { get; set; }
}
