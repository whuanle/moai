using System;
using System.Text.Json.Serialization;

namespace MoAI.Account.Queries.Responses;

/// <summary>
/// 用户列表项.
/// </summary>
public class QueryUserListCommandResponseItem
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long Id { get; init; }

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
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}
