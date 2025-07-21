// <copyright file="QueryAiModelProviderListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 系统视图，查看已添加的模型供应商和模型数量.
/// </summary>
public class QuerySystemAiModelProviderListCommand : IRequest<QueryAiModelProviderListResponse>
{
}
