// <copyright file="CreatePromptClassCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 创建提示词分类.
/// </summary>
public class CreatePromptClassCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;
}
