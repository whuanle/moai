using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 解析状态响应数据
/// </summary>
public class Doc2xParseStatusResponse : Doc2xCode
{
    /// <summary>
    /// 响应数据
    /// </summary>
    [JsonPropertyName("data")]
    public Doc2xParseStatusData Data { get; init; } = default!;
}