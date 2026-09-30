using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Zabbix 查询插件配置（每个实例独立保存）.
/// </summary>
public class ZabbixQueryConfig
{
    /// <summary>
    /// Zabbix 前端地址.
    /// </summary>
    [Description("Zabbix 前端地址，例如 http://zabbix.example.com；支持子路径部署，如 http://host/zabbix")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API 令牌（可选）.
    /// </summary>
    [Description("API 令牌（可选，Zabbix 5.4+ 的用户令牌）；已填时优先于用户名密码")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 用户名（可选）.
    /// </summary>
    [Description("用户名（可选；与密码配合走 user.login 换取会话，每次运行重新登录）")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码（可选）.
    /// </summary>
    [Description("密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 鉴权凭证放置位置.
    /// </summary>
    [Description("鉴权凭证放 HTTP Authorization: Bearer 头（Zabbix 6.4+ 推荐形态）。默认 false 放 JSON-RPC auth 属性（兼容 5.x-7.x）；7.0+ 建议改 true")]
    public bool UseHeaderAuth { get; set; }

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 列表类结果最多返回条数（1-500）.
    /// </summary>
    [Description("problems/hosts/triggers 等列表最多返回条数，取值 1-500（默认 100）；同时作为下发给 Zabbix 的 limit")]
    public int MaxListItems { get; set; } = 100;
}
