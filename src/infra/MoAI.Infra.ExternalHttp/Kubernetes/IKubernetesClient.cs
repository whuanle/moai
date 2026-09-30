using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.Kubernetes;

/// <summary>
/// Kubernetes API 客户端接口：只读 GET，Bearer（ServiceAccount/用户令牌）鉴权.
/// </summary>
/// <remarks>
/// K8s API Server 证书几乎总是私有 CA 签发，实例配置的「跳过 TLS 校验」必须按请求生效，无法用共享
/// 具名客户端承载，因此这里按调用注入 <see cref="ExternalHttpMessageHandler"/>（保留统一外部请求日志与遥测）
/// 并在其下挂按次创建的 <see cref="System.Net.Http.HttpClientHandler"/>.
/// </remarks>
public interface IKubernetesClient
{
    /// <summary>
    /// GET 一个 API 端点（含查询串的完整地址）.
    /// </summary>
    /// <param name="endpoint">完整端点地址（如 <c>https://api.k8s:6443/api/v1/pods?limit=100</c>）.</param>
    /// <param name="bearerToken">Bearer 令牌（ServiceAccount JWT 等，可空=匿名）.</param>
    /// <param name="skipTlsVerify">是否跳过 TLS 证书校验（自签 CA 的 API Server）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应原文（JSON 或 Pod 日志纯文本）.</returns>
    Task<string> GetAsync(Uri endpoint, string? bearerToken, bool skipTlsVerify, CancellationToken cancellationToken = default);
}
