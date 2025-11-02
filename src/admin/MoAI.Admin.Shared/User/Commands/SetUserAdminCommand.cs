using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 设置用户是否为管理员.
/// </summary>
public class SetUserAdminCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 是否为管理员.
    /// </summary>
    public bool IsAdmin { get; init; }
}