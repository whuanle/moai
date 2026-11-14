using MoAI.Infra.Helpers;
using MoAI.Infra.Models;

namespace MoAI;

/// <summary>
/// ClientContextExtensions.
/// </summary>
public static class ClientContextExtensions
{
    /// <summary>
    /// 设置客户端信息（来源 IP 与 UserAgent）.
    /// </summary>
    /// <typeparam name="T">IClientContext.</typeparam>
    /// <param name="obj"></param>
    /// <param name="ip">客户端 IP.</param>
    /// <param name="userAgent">客户端 UserAgent.</param>
    public static void SetClientInfo<T>(this T obj, string ip, string userAgent)
        where T : class, IClientContext
    {
        obj.SetProperty(o => o.ContextClientIp, ip);
        obj.SetProperty(o => o.ContextUserAgent, userAgent);
    }
}
