using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 注册用户.
/// </summary>
public class RegisterUserCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 邮箱.
    /// </summary>
    public string Email { get; init; } = default!;

    /// <summary>
    /// 密码，接口请求时，使用 RSA 公钥加密密码.
    /// </summary>
    public string Password { get; init; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; init; } = default!;

    /// <summary>
    /// 手机号.
    /// </summary>
    public string Phone { get; init; } = default!;
}