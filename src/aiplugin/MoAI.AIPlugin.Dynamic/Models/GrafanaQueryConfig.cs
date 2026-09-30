using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Grafana 查询插件配置（每个实例独立保存）.
/// </summary>
public class GrafanaQueryConfig
{
    /// <summary>
    /// Grafana 地址.
    /// </summary>
    [Description("Grafana 地址，例如 http://grafana.example.com；支持子路径部署，如 http://host/grafana")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 服务账号令牌（可选）.
    /// </summary>
    [Description("服务账号令牌（可选，glsa_... 开头）；已填时优先于用户名密码")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Basic 用户名（可选）.
    /// </summary>
    [Description("Basic 认证用户名（可选）；用户名密码与 Token 都未填则匿名")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic 密码（可选）.
    /// </summary>
    [Description("Basic 认证密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 列表最多返回条数（1-500）.
    /// </summary>
    [Description("注解/仪表板列表最多返回条数，取值 1-500（默认 100）；同时作为下发给 Grafana 的 limit")]
    public int MaxListItems { get; set; } = 100;
}
