using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 解绑第三方账号.
/// </summary>
public class UnbindUserOAuthCommand : IRequest<EmptyCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 第三方认证方式 id，对应 OauthConnection 表的 id.
    /// </summary>
    public Guid ProviderId { get; set; }
}
