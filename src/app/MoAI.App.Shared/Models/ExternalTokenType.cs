using System.Text.Json.Serialization;

namespace MoAI.App.Models;

/// <summary>
/// 外部 token 类型.
/// </summary>
public enum ExternalTokenType
{
    /// <summary>
    /// 应用 token，代表应用接入（access_app）本身，授权范围为接入配置的全部应用.
    /// </summary>
    [JsonPropertyName("app")]
    App = 0,

    /// <summary>
    /// 用户 token，绑定一个外部用户 id 且仅授权单个应用.
    /// </summary>
    [JsonPropertyName("user")]
    User = 1,
}
