using MoAI.Database.Entities;

namespace MoAI.Org.Tests;

/// <summary>
/// 组织角色单测种子数据构造.
/// </summary>
internal static class OrgTestSeed
{
    /// <summary>
    /// 构造组织角色（含自增 id 显式落库，path 与 id 链保持一致）.
    /// </summary>
    public static OrgRoleEntity Role(int id, string name, int? parentId, string path) => new()
    {
        Id = id,
        Name = name,
        ParentId = parentId,
        Path = path,
    };

    /// <summary>
    /// 构造用户（字符串列全填，sqlite 非空约束兜底）.
    /// </summary>
    public static UserEntity User(long id, string userName, string nickName) => new()
    {
        Id = id,
        UserName = userName,
        NickName = nickName,
        Email = $"{userName}@test.local",
        Password = string.Empty,
        PasswordSalt = string.Empty,
        AvatarPath = string.Empty,
        Phone = string.Empty,
    };

    /// <summary>
    /// 构造用户-角色绑定.
    /// </summary>
    public static OrgUserRoleEntity Binding(long userId, int roleId) => new()
    {
        UserId = userId,
        RoleId = roleId,
    };
}
