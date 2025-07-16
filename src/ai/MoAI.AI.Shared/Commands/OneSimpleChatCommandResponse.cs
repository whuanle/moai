// <copyright file="OneSimpleChatCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;

namespace MoAI.AI.Commands;

/// <summary>
/// AI 回答.
/// </summary>
public class OneSimpleChatCommandResponse
{
    /// <summary>
    /// 回复内容.
    /// </summary>
    public string Content { get; init; } = default!;
}