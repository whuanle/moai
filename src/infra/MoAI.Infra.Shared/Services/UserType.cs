using System.Text.Json.Serialization;

namespace MoAI.Infra.Models;

/// <summary>
/// 用户类型.
/// </summary>
public enum UserType
{
    /// <summary>
    /// 识别不到.
    /// </summary>
    [JsonPropertyName("none")]
    None = 0,

    /// <summary>
    /// 外部用户.
    /// </summary>
    [JsonPropertyName("external")]
    External = 1,

    /// <summary>
    /// 外部应用，包括 MCP 这些.
    /// </summary>
    [JsonPropertyName("externalapp")]
    ExternalApp = 2,

    /// <summary>
    /// 普通用户，内部无限制.
    /// </summary>
    [JsonPropertyName("normal")]
    Normal = 3,
}