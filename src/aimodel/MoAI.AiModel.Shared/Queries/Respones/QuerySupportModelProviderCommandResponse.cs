// <copyright file="QuerySupportModelProviderCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.AiModel.Models;

namespace MoAI.AiModel.Queries.Respones;

public class QuerySupportModelProviderCommandResponse
{
    /// <summary>
    /// 支持的模型供应商列表.
    /// </summary>
    public IReadOnlyCollection<AiProviderInfo> Providers { get; init; } = new List<AiProviderInfo>();
}