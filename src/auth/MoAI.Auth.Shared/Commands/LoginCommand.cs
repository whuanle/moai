using MediatR;
using MoAI.Auth.Commands.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 登录.
/// </summary>
public class LoginCommand : IRequest<LoginCommandResponse>, IClientContext
{
    /// <summary>
    /// 用户名或邮箱.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 密码，使用 RSA 公钥加密.
    /// </summary>
    public string Password { get; init; } = default!;

    /// <summary>
    /// 客户端 IP，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextClientIp { get; init; }

    /// <summary>
    /// 客户端 UserAgent，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextUserAgent { get; init; }
}
