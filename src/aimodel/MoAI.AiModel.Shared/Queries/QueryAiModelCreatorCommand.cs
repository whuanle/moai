// <copyright file="QueryAiModelCreatorCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询模型的创建者.
/// </summary>
public class QueryAiModelCreatorCommand : IRequest<QueryAiModelCreatorCommandResponse>
{
    /// <summary>
    /// 模型id.
    /// </summary>
    public int ModelId { get; init; }
}
