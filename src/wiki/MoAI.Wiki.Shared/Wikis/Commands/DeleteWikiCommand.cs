// <copyright file="DeleteWikiCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 删除知识库.
/// </summary>
public class DeleteWikiCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }
}
