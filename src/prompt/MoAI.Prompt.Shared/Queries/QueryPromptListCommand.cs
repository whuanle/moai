// <copyright file="QueryPromptListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAIPrompt.Queries.Responses;
using MediatR;

namespace MoAIPrompt.Queries;

/// <summary>
/// 查询能看到的提示词列表.
/// </summary>
public class QueryPromptListCommand : IRequest<QueryPromptListCommandResponse>
{
    /// <summary>
    /// 筛选分类.
    /// </summary>
    public int? ClassId { get; init; }

    /// <summary>
    /// 筛选名称.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 自己创建的.
    /// </summary>
    public bool? IsOwn { get; init; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
