using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 删除认证方式.
/// </summary>
public class DeleteOAuthConnectionCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthConnectionId { get; init; }
}