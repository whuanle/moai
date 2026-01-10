using Refit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// OCR操作结果详情.
/// </summary>
public class PaddleOcrResult
{
    /// <summary>
    /// OCR结果列表.
    /// </summary>
    [JsonPropertyName("ocrResults")]
    public List<PaddleOcrResultItem>? OcrResults { get; set; }

    /// <summary>
    /// 输入数据信息.
    /// </summary>
    [JsonPropertyName("dataInfo")]
    public JsonElement? DataInfo { get; set; }
}
