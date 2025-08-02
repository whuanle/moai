// <copyright file="QueryPromptCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAIPrompt.Models;
using MediatR;

namespace MoAIPrompt.Queries;

/// <summary>
/// 获取提示词.
/// </summary>
public class QueryPromptCommand : IRequest<PromptItem>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
