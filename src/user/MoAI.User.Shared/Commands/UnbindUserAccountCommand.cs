using MediatR;
using MoAI.Infra.Models;

namespace MoAI.User.Commands;

/// <summary>
/// 解绑第三方账号.
/// </summary>
public class UnbindUserAccountCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 绑定 id.
    /// </summary>
    public int BindId { get; init; }
}