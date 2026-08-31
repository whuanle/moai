using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Account.Commands;

/// <summary>
/// 使用 OAuth 绑定已存在的账号.
/// </summary>
public class OAuthBindExistAccountCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid TempOAuthBindId { get; init; } = default!;
}
