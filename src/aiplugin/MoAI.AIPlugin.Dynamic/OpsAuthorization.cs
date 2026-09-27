using System;
using System.Text;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 智能运维观测插件（Prometheus / Elasticsearch / ClickHouse / Tempo）共用的 Authorization 头拼装：优先级 ApiKey → Bearer → Basic，全部未配置时返回 null（不发鉴权头）.
/// </summary>
internal static class OpsAuthorization
{
    /// <summary>
    /// 拼 Authorization 头.
    /// </summary>
    /// <param name="username">Basic 认证用户名.</param>
    /// <param name="password">Basic 认证密码.</param>
    /// <param name="bearerToken">Bearer Token（已填时优先于 Basic）.</param>
    /// <param name="apiKey">API Key（Elasticsearch 等，最高优先级）.</param>
    /// <returns>Authorization 头值；无可配置鉴权时为 null.</returns>
    public static string? Build(string? username, string? password, string? bearerToken, string? apiKey = null)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return $"ApiKey {apiKey.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            return $"Bearer {bearerToken.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            return $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password ?? string.Empty}"))}";
        }

        return null;
    }
}
