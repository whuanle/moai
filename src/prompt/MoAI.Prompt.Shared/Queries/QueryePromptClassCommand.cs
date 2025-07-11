// <copyright file="QueryePromptClassCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 查询提示词分类列表.
/// </summary>
public class QueryePromptClassCommand : IRequest<QueryePromptClassCommandResponse>
{
}