// <copyright file="QueryUserListCommandResponseItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;

namespace MoAI.Admin.User.Queries.Responses;

/// <summary>
/// QueryUserListCommandResponseItem.
/// </summary>
public class QueryUserListCommandResponseItem : AuditsInfo
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
}