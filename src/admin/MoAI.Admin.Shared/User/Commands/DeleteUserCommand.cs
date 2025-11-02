using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 删除用户.
/// </summary>
public class DeleteUserCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();
}
