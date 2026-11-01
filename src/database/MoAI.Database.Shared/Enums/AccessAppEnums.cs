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
/// 系统级接入类型：仅 level=system 行使用，子系统与应用接入同表分型.
/// </summary>
public enum AccessAppType
{
    /// <summary>
    /// 不适用（团队级行）.
    /// </summary>
    [JsonPropertyName("none")]
    None = 0,

    /// <summary>
    /// 子系统：MoAI 生态独立部署的服务，可配置委托 SSO 回调与团队/组织授权，具备心跳在线.
    /// </summary>
    [JsonPropertyName("subsystem")]
    Subsystem = 1,

    /// <summary>
    /// 应用接入：交给外部系统（ERP/OA 等）的凭证，无回调与授权配置，可全量访问 /api/subsys 系统资源.
    /// </summary>
    [JsonPropertyName("app")]
    App = 2,
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
