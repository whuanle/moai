using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.Zabbix;

/// <summary>
/// Zabbix JSON-RPC 客户端接口，端点地址由插件按实例配置拼装（支持前端子路径部署）.
/// </summary>
/// <remarks>
/// Refit 的方法路径必须以 <c>/</c> 开头、与 BaseAddress 子路径互斥，因此这里走
/// <see cref="System.Net.Http.IHttpClientFactory"/>（复用统一的外部请求日志与遥测 handler）手工拼端点.
/// </remarks>
public interface IZabbixClient
{
    /// <summary>
    /// POST 一条 JSON-RPC 请求.
    /// </summary>
    /// <param name="endpoint">完整端点地址（如 <c>http://host/zabbix/api_jsonrpc.php</c>）.</param>
    /// <param name="authorization">Authorization 头值（<c>Bearer &lt;token|session&gt;</c>，body auth 形态传 null）.</param>
    /// <param name="request">JSON-RPC 报文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应报文原文.</returns>
    Task<string> RpcAsync(Uri endpoint, string? authorization, ZabbixRpcRequest request, CancellationToken cancellationToken = default);
}
