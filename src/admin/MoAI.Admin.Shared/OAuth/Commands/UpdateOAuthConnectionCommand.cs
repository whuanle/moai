using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 更新 OAuth2.0 连接配置.
/// </summary>
public class UpdateOAuthConnectionCommand : CreateOAuthConnectionCommand, IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthConnectionId { get; init; }
}
