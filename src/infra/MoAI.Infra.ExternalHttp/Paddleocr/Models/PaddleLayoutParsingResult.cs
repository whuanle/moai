using Refit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr.Models;

/// <summary>
/// 文档解析结果详情.
/// </summary>
public class PaddleLayoutParsingResult
{
    /// <summary>
    /// 文档解析结果列表.
    /// </summary>
    [JsonPropertyName("layoutParsingResults")]
    public List<PaddleLayoutParsingResultItem>? LayoutParsingResults { get; set; }

    /// <summary>
    /// 输入数据信息.
    /// </summary>
    [JsonPropertyName("dataInfo")]
    public JsonElement? DataInfo { get; set; }

    /// <summary>
    /// 输入数据信息.
    /// </summary>
    [JsonPropertyName("ocrResults")]
    public List<JsonElement>? OcrResults { get; set; }
}
