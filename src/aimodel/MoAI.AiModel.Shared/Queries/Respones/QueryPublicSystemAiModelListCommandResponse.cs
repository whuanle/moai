// <copyright file="QueryAiModelListCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.AiModel.Models;

namespace MoAI.AiModel.Queries.Respones;

/// <summary>
/// Ai 模型列表.
/// </summary>
public class QueryPublicSystemAiModelListCommandResponse
{
    /// <summary>
    /// AI 模型列表.
    /// </summary>
    public IReadOnlyCollection<AiNotKeyEndpoint> AiModels { get; init; } = new List<AiNotKeyEndpoint>();
}