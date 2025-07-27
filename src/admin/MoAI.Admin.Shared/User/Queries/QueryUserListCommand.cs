// <copyright file="QueryUserListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Admin.User.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Queries;

/// <summary>
/// 分页查询用户列表.
/// </summary>
public class QueryUserListCommand : PagedParamter, IRequest<QueryUserListCommandResponse>
{
    /// <summary>
    /// 查询指定用户.
    /// </summary>
    public int? UserId { get; init; }

    /// <summary>
    /// 根据用户名筛选.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// 搜索参数.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// 只看管理员.
    /// </summary>
    public bool? IsAdmin { get; init; }
}
