// <copyright file="DeletePromptCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 删除提示词.
/// </summary>
public class DeletePromptCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }
}