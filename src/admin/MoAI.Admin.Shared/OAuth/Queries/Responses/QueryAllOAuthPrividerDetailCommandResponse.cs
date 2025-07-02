// <copyright file="QueryAllOAuthPrividerCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;

namespace MoAI.Admin.OAuth.Queries.Responses;

public class QueryAllOAuthPrividerDetailCommandResponse
{
    public List<QueryAllOAuthPrividerDetailCommandResponseItem> Items { get; set; } = new List<QueryAllOAuthPrividerDetailCommandResponseItem>();
}
