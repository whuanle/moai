using System.Text.Json.Serialization;

namespace MoAI.LoginSafety.Models;

/// <summary>
/// 登录风险等级.
/// </summary>
public enum LoginRiskLevel
{
    /// <summary>
    /// 无风险.
    /// </summary>
    [JsonPropertyName("none")]
    None = 0,

    /// <summary>
    /// 低风险（仅记录）.
    /// </summary>
    [JsonPropertyName("low")]
    Low = 1,

    /// <summary>
    /// 中风险（记风险事件，可给用户发提醒邮件）.
    /// </summary>
    [JsonPropertyName("medium")]
    Medium = 2,

    /// <summary>
    /// 高风险（记风险事件、给用户发提醒、给管理员发告警）.
    /// </summary>
    [JsonPropertyName("high")]
    High = 3,
}
