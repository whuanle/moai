namespace MoAI.Database.Enums;

/// <summary>
/// 团队成员角色.
/// </summary>
public enum TeamRole
{
    /// <summary>
    /// 所有者，团队创建者，可解散团队、转让所有权、管理一切.
    /// </summary>
    Owner = 0,

    /// <summary>
    /// 管理员，可管理成员、创建知识库/插件等团队资源.
    /// </summary>
    Admin = 1,

    /// <summary>
    /// 普通成员，可使用团队资源.
    /// </summary>
    Member = 2,
}
