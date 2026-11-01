using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

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
