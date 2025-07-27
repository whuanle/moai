// <copyright file="QueryAllOAuthPrividerDetailCommandResponseItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;

namespace MoAI.Admin.OAuth.Queries.Responses;

public class QueryAllOAuthPrividerDetailCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 提供商名称
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 提供商图标地址
    /// </summary>
    public string IconUrl { get; set; } = default!;

    /// <summary>
    /// 提供商标识
    /// </summary>
    public string Provider { get; set; } = default!;

    public string Key { get; set; } = default!;

    /// <summary>
    /// 发现端口
    /// </summary>
    public string WellKnown { get; set; } = default!;

    /// <summary>
    /// 回调地址.
    /// </summary>
    public string AuthorizeUrl { get; set; } = default!;
}
