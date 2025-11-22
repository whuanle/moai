using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 文件预上传响应数据
/// </summary>
public class Doc2xPreuploadResponse : Doc2xCode
{
    /// <summary>
    /// 数据体.
    /// </summary>
    [JsonPropertyName("data")]
    public Doc2xPreuploadData Data { get; init; } = default!;
}