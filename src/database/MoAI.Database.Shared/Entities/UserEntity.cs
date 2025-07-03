using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 用户.
/// </summary>
public partial class UserEntity : IFullAudited
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
    /// 密码.
    /// </summary>
    public string Password { get; set; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; set; } = default!;

    /// <summary>
    /// 头像路径.
    /// </summary>
    public string AvatarPath { get; set; } = default!;

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

    /// <summary>
    /// 计算密码值的salt.
    /// </summary>
    public string PasswordSalt { get; set; } = default!;

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
