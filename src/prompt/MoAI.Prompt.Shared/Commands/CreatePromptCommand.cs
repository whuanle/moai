// <copyright file="CreatePromptCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 创建提示词.
/// </summary>
public class CreatePromptCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 分类.
    /// </summary>
    public int PromptClassId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 助手设定,支持 markdown.
    /// </summary>
    public string Content { get; init; } = default!;

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }
}
