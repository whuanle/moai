using MediatR;
using MoAI.Infra.Models;

namespace MoAI.User.Commands;

/// <summary>
/// 修改用户信息.
/// </summary>
public class UpdateUserInfoCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 邮箱.
    /// </summary>
    public string Email { get; set; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; set; } = default!;

    /// <summary>
    /// 手机号.
    /// </summary>
    public string Phone { get; set; } = default!;

    /// <summary>
    /// 头像.
    /// </summary>
    public string AvatarPath { get; set; } = default!;
}
