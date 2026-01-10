using Refit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// 单个OCR结果项.
/// </summary>
public class PaddleOcrResultItem
{
    /// <summary>
    /// 简化版的预测结果.
    /// </summary>
    [JsonPropertyName("prunedResult")]
    public JsonElement? PrunedResult { get; set; }

    /// <summary>
    /// OCR结果图 (Base64).
    /// </summary>
    [JsonPropertyName("ocrImage")]
    public string? OcrImage { get; set; }

    /// <summary>
    /// 可视化结果图像 (Base64).
    /// </summary>
    [JsonPropertyName("docPreprocessingImage")]
    public string? DocPreprocessingImage { get; set; }

    /// <summary>
    /// 输入图像 (Base64).
    /// </summary>
    [JsonPropertyName("inputImage")]
    public string? InputImage { get; set; }
}