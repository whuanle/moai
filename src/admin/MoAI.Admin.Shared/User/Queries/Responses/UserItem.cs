using MoAI.Infra.Models;

namespace MoAI.Admin.User.Queries.Responses;

/// <summary>
/// QueryUserListCommandResponseItem.
/// </summary>
public class UserItem : AuditsInfo
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public int Id { get; set; }

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
    /// 禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否管理员.
    /// </summary>
    public bool IsAdmin { get; set; }
}