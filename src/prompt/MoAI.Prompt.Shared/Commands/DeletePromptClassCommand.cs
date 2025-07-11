// <copyright file="DeletePromptClassCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 删除提示词分类.
/// </summary>
public class DeletePromptClassCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// id.
    /// </summary>
    public int ClassId { get; init; }
}
