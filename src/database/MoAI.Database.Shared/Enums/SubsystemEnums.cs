using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 子系统状态.
/// </summary>
public enum SubsystemStatus
{
    /// <summary>
    /// 启用：子系统可换取 token 并接入.
    /// </summary>
    [JsonPropertyName("enabled")]
    Enabled = 0,

    /// <summary>
    /// 停用：拒绝换取 token，已签发 token 的机器面调用一并拒绝.
    /// </summary>
    [JsonPropertyName("disabled")]
    Disabled = 1,
}

/// <summary>
/// 子系统团队授权范围.
/// </summary>
public enum SubsystemTeamScope
{
    /// <summary>
    /// 全员可用：所有团队均可使用该子系统.
    /// </summary>
    [JsonPropertyName("allTeams")]
    AllTeams = 0,

    /// <summary>
    /// 指定团队：仅授权列表中的团队可使用该子系统.
    /// </summary>
    [JsonPropertyName("specifiedTeams")]
    SpecifiedTeams = 1,
}
