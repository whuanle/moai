using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 禁用启用用户.
/// </summary>
public class DisableUserCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }
}
