// <copyright file="QueryAiModelProviderListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Queries.Respones;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询 ai 模型供应商和已添加的ai模型数量列表.
/// </summary>
public class QueryAiModelProviderListCommand : IRequest<QueryAiModelProviderListResponse>
{
}
