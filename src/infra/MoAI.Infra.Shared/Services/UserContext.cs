// <copyright file="UserContext.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// 用户上下文接口，提供当前用户的信息.
/// </summary>
public abstract class UserContext
{
    /// <summary>
    /// 当前用户是否已认证，用户可能是匿名访问.
    /// </summary>
    public virtual bool IsAuthenticated { get; init; }

    /// <summary>
    /// 用户ID.
    /// </summary>
    public virtual int UserId { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public virtual string UserName { get; init; } = default!;

    /// <summary>
    /// 用户昵称.
    /// </summary>
    public virtual string NickName { get; init; } = default!;

    /// <summary>
    /// 用户邮箱.
    /// </summary>
    public virtual string Email { get; init; } = default!;
}
