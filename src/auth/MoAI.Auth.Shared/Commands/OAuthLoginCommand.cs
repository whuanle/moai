using MediatR;
using MoAI.Auth.Commands.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Commands;

/// <summary>
/// 使用 OAuth 登录，用于第三方登录回调后触发接口.
/// </summary>
public class OAuthLoginCommand : IRequest<OAuthLoginCommandResponse>, IClientContext
{
    /// <summary>
    /// Code.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthId { get; init; } = default!;

    /// <summary>
    /// 授权发起页来源（如 https://example.com），回调地址与飞书换 token 的 redirect_uri 需与之一致；为空回退 WebUI.
    /// </summary>
    public Uri? RedirectUrl { get; init; }

    /// <summary>
    /// 客户端 IP，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextClientIp { get; init; }

    /// <summary>
    /// 客户端 UserAgent，通过上下文自动配置，前端不需要传递.
    /// </summary>
    public string? ContextUserAgent { get; init; }
}
