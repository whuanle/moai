// <copyright file="OneSimpleChatCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;

namespace MoAI.AI.Commands;

/// <summary>
/// 一次简单的提问.
/// </summary>
public class OneSimpleChatCommand : IRequest<OneSimpleChatCommandResponse>
{
    /// <summary>
    /// 对话 AI 信息.
    /// </summary>
    public AiEndpoint Endpoint { get; init; } = default!;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string Prompt { get; init; } = default!;

    /// <summary>
    /// 问题.
    /// </summary>
    public string Question { get; init; } = default!;
}
