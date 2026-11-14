using MediatR;
using MoAI.Auth.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Auth.Queries;

/// <summary>
/// 获取新的图形验证码（匿名），返回验证码 id 与 SVG 图片（base64）.
/// </summary>
public class QueryCaptchaNewQuery : IRequest<QueryCaptchaNewQueryResponse>, IClientContext
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
