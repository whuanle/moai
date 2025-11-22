using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;

/// <summary>
/// 状态码字段.
/// </summary>
public class Doc2xCode
{
    /// <summary>
    /// 正常应该是 "success".
    /// </summary>
    [JsonPropertyName("code")]
    [Description("正常应该是 \"success\"")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// 错误信息.
    /// </summary>
    [JsonPropertyName("msg")]
    [Description("错误信息")]
    public string? Msg { get; init; } = string.Empty;
}
