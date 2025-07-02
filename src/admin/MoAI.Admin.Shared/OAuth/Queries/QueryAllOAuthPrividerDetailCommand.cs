// <copyright file="QueryAllOAuthPrividerDetailCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Admin.OAuth.Queries.Responses;

namespace MoAI.Login.Queries;

/// <summary>
/// 获取所有 OAuth 提供者详细信息。
/// </summary>
public class QueryAllOAuthPrividerDetailCommand : IRequest<QueryAllOAuthPrividerDetailCommandResponse>
{
}
