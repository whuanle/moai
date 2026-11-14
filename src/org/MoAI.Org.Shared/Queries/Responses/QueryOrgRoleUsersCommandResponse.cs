using System;
using MoAI.Infra.Models;

namespace MoAI.Org.Queries.Responses;

/// <summary>
/// 角色下用户项（用户资料字段与用户管理列表对齐）.
/// </summary>
public class QueryOrgRoleUsersCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; init; } = string.Empty;

    /// <summary>
    /// 邮箱.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// 手机号.
    /// </summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    /// 头像地址.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 是否管理员.
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// 是否超级管理员.
    /// </summary>
    public bool IsRoot { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <summary>
    /// 加入该组织的时间（绑定行创建时间）.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}

/// <summary>
/// 角色下用户清单结果.
/// </summary>
public class QueryOrgRoleUsersCommandResponse
{
    /// <summary>
    /// 用户列表，按绑定时间升序.
    /// </summary>
    public List<QueryOrgRoleUsersCommandResponseItem> Items { get; init; } = [];
}
