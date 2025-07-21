// <copyright file="QueryUserIsPluginCreatorCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;

namespace MoAI.Plugin.Queries.Responses;

public class QueryPluginCreatorCommandResponse
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;

    public bool Exist { get; init; }
    public bool IsSystem { get; init; }
    public int CreatorId { get; init; }
}