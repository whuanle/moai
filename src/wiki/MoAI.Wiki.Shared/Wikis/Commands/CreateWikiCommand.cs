// <copyright file="CreateWikiCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 创建知识库.
/// </summary>
public class CreateWikiCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 团队描述.
    /// </summary>
    public string Description { get; set; } = default!;
}
