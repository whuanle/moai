using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.ClickStack;

/// <summary>
/// ClickStack（HyperDX）对外 API 客户端接口，端点地址由插件按实例配置拼装（API server 默认 8000 端口，支持任意端口偏移与子路径部署）.
/// </summary>
/// <remarks>
/// 与 <see cref="MoAI.Infra.Grafana.IGrafanaClient"/> 同款形态：Refit 方法路径必须以 <c>/</c> 开头且与
/// BaseAddress 子路径互斥，因此走 <see cref="System.Net.Http.IHttpClientFactory"/>（复用统一的外部请求日志与遥测）手工拼端点；
/// 响应为原文，解析由插件完成.
/// </remarks>
public interface IClickStackClient
{
    /// <summary>
    /// GET 一个 API 端点（如 <c>api/v2/sources</c>）.
    /// </summary>
    /// <param name="endpoint">完整端点地址.</param>
    /// <param name="authorization">Authorization 头值（<c>Bearer &lt;Personal API Access Key&gt;</c>）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应原文.</returns>
    Task<string> GetAsync(Uri endpoint, string authorization, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST 一个 JSON API 端点（如 <c>api/v2/search</c>、<c>api/v2/charts/series</c>）.
    /// </summary>
    /// <param name="endpoint">完整端点地址.</param>
    /// <param name="authorization">Authorization 头值（<c>Bearer &lt;Personal API Access Key&gt;</c>）.</param>
    /// <param name="jsonBody">JSON 请求体原文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应原文.</returns>
    Task<string> PostJsonAsync(Uri endpoint, string authorization, string jsonBody, CancellationToken cancellationToken = default);
}
