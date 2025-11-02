using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 重置用户密码.
/// </summary>
public class ResetUserPasswordCommand : IRequest<SimpleString>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
