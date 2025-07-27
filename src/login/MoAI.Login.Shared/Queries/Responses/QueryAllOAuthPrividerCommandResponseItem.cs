// <copyright file="QueryAllOAuthPrividerCommandResponseItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Login.Queries.Responses;

/// <summary>
/// QueryAllOAuthPrividerCommandResponseItem.
/// </summary>
public class QueryAllOAuthPrividerCommandResponseItem
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthId { get; init; } = default!;

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

    /// <summary>
    /// 授权地址.
    /// </summary>
    public string RedirectUrl { get; set; } = default!;
}
