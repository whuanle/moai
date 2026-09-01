using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 已登录用户通过第三方授权回调 code 直接绑定第三方账号（与登录接口分离）.
/// </summary>
public class OAuthBindAccountByCodeCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 第三方认证方式 id，对应 OauthConnection 表的 id.
    /// </summary>
    public Guid OAuthId { get; init; } = default!;

    /// <summary>
    /// 第三方授权回调得到的 code.
    /// </summary>
    public string Code { get; init; } = default!;
}
