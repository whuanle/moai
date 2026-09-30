using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.Alertmanager;

/// <summary>
/// Alertmanager HTTP API 客户端接口，端点地址由插件按实例配置拼装（支持子路径部署，如 <c>http://host/alertmanager</c>）.
/// </summary>
/// <remarks>
/// 与 <see cref="MoAI.Infra.Grafana.IGrafanaClient"/> 同款形态：Refit 方法路径必须以 <c>/</c> 开头且与
/// BaseAddress 子路径互斥，因此走 <see cref="System.Net.Http.IHttpClientFactory"/>（复用统一的外部请求日志与遥测）手工拼端点；
/// 响应为原文，解析由插件完成.
/// </remarks>
public interface IAlertmanagerClient
{
    /// <summary>
    /// GET 一个 API 端点（含查询串的完整地址）.
    /// </summary>
    /// <param name="endpoint">完整端点地址（如 <c>http://host/alertmanager/api/v2/alerts</c>）.</param>
    /// <param name="authorization">Authorization 头（可空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应原文.</returns>
    Task<string> GetAsync(Uri endpoint, string? authorization, CancellationToken cancellationToken = default);
}
