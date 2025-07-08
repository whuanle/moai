// <copyright file="InviteWikiUserCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 移除知识库协作成员.
/// </summary>
public class RemoveWikiUserCommand : IRequest<EmptyCommandResponse>
{
    public int WikiId { get; init; }

    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();
}