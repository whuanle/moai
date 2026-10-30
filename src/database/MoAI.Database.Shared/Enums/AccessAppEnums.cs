using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 应用接入层级：同一张 access_app 表承载两类资源域的接入，层级决定可访问的资源面.
/// </summary>
public enum AccessAppLevel
{
    /// <summary>
    /// 团队级（moai-ac- 前缀）：只能访问所属团队的资源（/api/external/*、模型网关）.
    /// </summary>
    [JsonPropertyName("team")]
    Team = 0,

    /// <summary>
    /// 系统级（moai-sa- 前缀）：不能访问团队资源，只能访问系统资源（/api/subsys/system/*，组织/用户/团队治理）.
    /// </summary>
    [JsonPropertyName("system")]
    System = 1,
}

/// <summary>
/// 应用接入状态.
/// </summary>
public enum AccessAppStatus
{
    /// <summary>
    /// 启用.
    /// </summary>
    [JsonPropertyName("enabled")]
    Enabled = 0,

    /// <summary>
    /// 停用（协作面调用立即拒绝）.
    /// </summary>
    [JsonPropertyName("disabled")]
    Disabled = 1,
}
