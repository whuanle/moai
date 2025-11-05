using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 解析状态响应数据
/// </summary>
public class Doc2xParseStatusResponse
{
    /// <summary>
    /// 响应状态码
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; }

    /// <summary>
    /// 响应数据
    /// </summary>
    [JsonPropertyName("data")]
    public Doc2xParseStatusData Data { get; set; }
}