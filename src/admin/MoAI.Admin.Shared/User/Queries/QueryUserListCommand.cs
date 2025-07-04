// <copyright file="QueryUserListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;

namespace MoAI.Admin.User.Queries;

/// <summary>
/// 查询用户列表.
/// </summary>
public class QueryUserListCommand : PagedParamter
{
    /// <summary>
    /// 指定用户的 id.
    /// </summary>
    public int? UserId { get; init; }

    /// <summary>
    /// 搜索参数.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// 只看管理员.
    /// </summary>
    public bool? IsAdmin { get; init; }
}
