using Maomi;
using Microsoft.AspNetCore.Http;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Infra.Defaults;

[InjectOnScoped]
public class ClientInfoProvider : IClientInfoProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Lazy<ClientInfo> _clientInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientInfoProvider"/> class.
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    public ClientInfoProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _clientInfo = new Lazy<ClientInfo>(() =>
         {
             return ParseClientInfo();
         });
    }

    /// <inheritdoc/>
    public ClientInfo GetClientInfo() => _clientInfo.Value;

    private ClientInfo ParseClientInfo()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            throw new BusinessException("HttpContext is not available.");
        }

        // Kestrel 双栈监听下 IPv4 连接表现为 IPv4-mapped IPv6（::ffff:a.b.c.d），统一映射回 IPv4，
        // 保证登录日志/风控比对/IP 封禁/限流的 IP 字符串单一形态（::1 等纯 IPv6 保持原样）
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null && remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        var ip = remoteIp?.ToString() ?? "Unknown";
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";

        return new ClientInfo
        {
            IP = ip,
            UserAgent = userAgent
        };
    }
}
