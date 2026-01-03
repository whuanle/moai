using MoAI.Infra.Paddleocr.Models;
using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Paddleocr;

/// <summary>
/// OCR响应结果.
/// </summary>
/// <typeparam name="TData">数据</typeparam>
public class PaddleOcrResponse<TData>
{
    /// <summary>
    /// 请求的UUID.
    /// </summary>
    [JsonPropertyName("logId")]
    public string? LogId { get; set; }
    /// <summary>
    /// 错误码. 固定为0表示成功.
    /// </summary>
    [JsonPropertyName("errorCode")]
    public int ErrorCode { get; set; }
    /// <summary>
    /// 错误说明. 固定为"Success"表示成功.
    /// </summary>
    [JsonPropertyName("errorMsg")]
    public string? ErrorMsg { get; set; }
    /// <summary>
    /// 操作结果.
    /// </summary>
    [JsonPropertyName("result")]
    public TData? Result { get; set; }
}
