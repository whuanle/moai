// <copyright file="QueryAiModelListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询模型列表.
/// </summary>
public class QueryAiModelListCommand : IRequest<QueryAiModelListCommandResponse>
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
