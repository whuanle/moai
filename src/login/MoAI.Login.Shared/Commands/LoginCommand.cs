using MediatR;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 登录.
/// </summary>
public class LoginCommand : IRequest<LoginCommandResponse>
{
    /// <summary>
    /// 用户名或邮箱.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 密码，使用 RSA 公钥加密.
    /// </summary>
    public string Password { get; init; } = default!;
}
