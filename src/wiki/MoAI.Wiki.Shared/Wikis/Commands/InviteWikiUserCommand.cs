// <copyright file="InviteWikiUserCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 邀请知识库协作成员，可以管理知识库文档等.
/// </summary>
public class InviteWikiUserCommand : IRequest<EmptyCommandResponse>
{
    public int UserId { get; init; }
    public int WikiId { get; init; }
}
