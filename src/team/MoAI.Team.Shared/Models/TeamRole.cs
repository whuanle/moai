namespace MoAI.Team.Models;

/// <summary>
/// 团队角色枚举.
/// </summary>
public enum TeamRole : int
{
    /// <summary>
    /// 无角色/不在团队.
    /// </summary>
    None = 0,

    /// <summary>
    /// 协作者.
    /// </summary>
    Collaborator = 1,

    /// <summary>
    /// 管理员.
    /// </summary>
    Admin = 2,

    /// <summary>
    /// 所有者.
    /// </summary>
    Owner = 3
}
