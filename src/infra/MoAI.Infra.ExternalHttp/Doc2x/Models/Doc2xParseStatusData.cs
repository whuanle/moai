using System.Text.Json.Serialization;

namespace MoAI.Infra.Doc2x.Models;


/// <summary>
/// 解析状态数据
/// </summary>
public class Doc2xParseStatusData
{
    /// <summary>
    /// 任务进度，0~100 的整数
    /// </summary>
    [JsonPropertyName("progress")]
    public int Progress { get; init; } = default!;

    /// <summary>
    /// 任务状态（processing、failed、success）
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = default!;

    /// <summary>
    /// 任务失败时的详细信息
    /// </summary>
    [JsonPropertyName("detail")]
    public string Detail { get; init; } = default!;

    /// <summary>
    /// 解析结果
    /// </summary>
    [JsonPropertyName("result")]
    public Doc2xParseResult Result { get; init; } = default!;
}