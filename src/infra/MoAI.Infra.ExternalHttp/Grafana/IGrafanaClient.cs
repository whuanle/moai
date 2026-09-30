using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.Grafana;

/// <summary>
/// Grafana HTTP API 客户端接口，端点地址由插件按实例配置拼装（支持子路径部署，如 <c>http://host/grafana</c>）.
/// </summary>
/// <remarks>
/// 与 <see cref="MoAI.Infra.Zabbix.IZabbixClient"/> 同款形态：Refit 方法路径必须以 <c>/</c> 开头且与
/// BaseAddress 子路径互斥，因此走 <see cref="System.Net.Http.IHttpClientFactory"/>（复用统一的外部请求日志与遥测）手工拼端点；
/// 响应为原文，解析由插件完成.
/// </remarks>
public interface IGrafanaClient
{
    /// <summary>
    /// GET 一个 API 端点（含查询串的完整地址）.
    /// </summary>
    /// <param name="endpoint">完整端点地址（如 <c>http://host/grafana/api/annotations?from=1</c>）.</param>
    /// <param name="authorization">Authorization 头（<see cref="MoAI.AIPlugin.Dynamic"/> 侧由 OpsAuthorization 拼，可空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应原文.</returns>
    Task<string> GetAsync(Uri endpoint, string? authorization, CancellationToken cancellationToken = default);
}
