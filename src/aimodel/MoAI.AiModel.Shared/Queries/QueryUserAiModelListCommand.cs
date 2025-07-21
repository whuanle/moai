// <copyright file="QueryUserViewAiModelListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 用户视图，查询用户能够看到的模型列表.
/// </summary>
public class QueryUserAiModelListCommand : IRequest<QueryAiModelListCommandResponse>
{
    /// <summary>
    /// AI 模型类型.
    /// </summary>
    public AiProvider? Provider { get; init; }

    /// <summary>
    /// Ai 模型类型.
    /// </summary>
    public AiModelType? AiModelType { get; init; }
}
