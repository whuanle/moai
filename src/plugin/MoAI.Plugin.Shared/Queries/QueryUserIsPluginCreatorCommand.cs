// <copyright file="QueryUserIsPluginCreatorCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 用户是否为该插件的创建者.
/// </summary>
public class QueryUserIsPluginCreatorCommand : IRequest<SimpleBool>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; } = default!;

    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;
}
