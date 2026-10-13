using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 定时器触发方式.
/// </summary>
public enum TimerTriggerType
{
    /// <summary>
    /// 按计划调度（Hangfire 定时触发）.
    /// </summary>
    [JsonPropertyName("scheduled")]
    Scheduled = 0,

    /// <summary>
    /// 手动触发（立即执行）.
    /// </summary>
    [JsonPropertyName("manual")]
    Manual = 1,
}
