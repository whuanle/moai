using MediatR;
using MoAI.Infra.Models;

namespace MoAI.User.Commands;

/// <summary>
/// 重置密码.
/// </summary>
public class UpdateUserPasswordCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 新的密码，提前使用 rsa 公有加密.
    /// </summary>
    public string Password { get; init; } = default!;
}