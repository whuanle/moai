// <copyright file="ChatContentItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 对话项.
/// </summary>
public class ChatContentItem
{
    public long RecordId { get; init; }

    /// <summary>
    /// 角色名称，system、assistant、user、tool.
    /// </summary>
    public string AuthorName { get; init; } = default!;

    /// <summary>
    /// 对话内容.
    /// </summary>
    public string? Content { get; init; }
}
