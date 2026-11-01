namespace MoAI.Account.Queries.Responses;

/// <summary>
/// UserStateInfo.
/// </summary>
public class UserStateInfo
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public long UserId { get; set; }

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
    /// 是否超级管理员.
    /// </summary>
    public bool IsRoot { get; set; }

    /// <summary>
    /// 已被删除.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 是否已绑定并启用两步验证（TOTP）.
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// 是否待绑定两步验证（系统强制 2FA 且 token 携带 mfa_pending 且尚未绑定）：仅 /account/userinfo 响应中回填.
    /// </summary>
    public bool MfaPending { get; set; }
}
