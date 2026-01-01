namespace MoAI.Login.Queries.Responses;

/// <summary>
/// UserStateInfo.
/// </summary>
public class UserStateInfo
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
    /// 头像地址.
    /// </summary>
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否管理员.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 已被删除.
    /// </summary>
    public bool IsDeleted { get; set; }
}