// <copyright file="PromptItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;

namespace MaomiAI.Prompt.Models;

/// <summary>
/// 查询提示词详细信息.
/// </summary>
public class PromptItem : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 助手设定,markdown.
    /// </summary>
    public string? Content { get; set; } = default!;

    public bool IsPublic { get; set; }
    public int PromptClassId { get; set; }
}