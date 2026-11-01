namespace MoAI.Infra.Models;

/// <summary>
/// 客户端上下文（来源 IP 与 UserAgent），在 ASP.NET Core 请求管道中被自动赋值.
/// </summary>
public interface IClientContext
{
    /// <summary>
    /// 客户端 IP，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextClientIp { get; init; }

    /// <summary>
    /// 客户端 UserAgent，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextUserAgent { get; init; }
}
