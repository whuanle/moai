using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 定时器执行状态.
/// </summary>
public enum TimerExecutionStatus
{
    /// <summary>
    /// 等待执行（已入队）.
    /// </summary>
    [JsonPropertyName("pending")]
    Pending = 0,

    /// <summary>
    /// 执行中.
    /// </summary>
    [JsonPropertyName("running")]
    Running = 1,

    /// <summary>
    /// 执行成功.
    /// </summary>
    [JsonPropertyName("success")]
    Success = 2,

    /// <summary>
    /// 执行失败（重试已用尽）.
    /// </summary>
    [JsonPropertyName("failed")]
    Failed = 3,

    /// <summary>
    /// 失败待重试.
    /// </summary>
    [JsonPropertyName("retrying")]
    Retrying = 4,
}
