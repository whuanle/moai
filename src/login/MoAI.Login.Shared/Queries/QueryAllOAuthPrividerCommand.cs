using MediatR;
using MoAI.Login.Queries.Responses;

namespace MoAI.Login.Queries;

/// <summary>
/// 查询支持的 OAuth 登录方式.
/// </summary>
public class QueryAllOAuthPrividerCommand : IRequest<QueryAllOAuthPrividerCommandResponse>
{
    /// <summary>
    /// 要跳转的路径.
    /// </summary>
    public Uri? RedirectUrl { get; init; }
}
