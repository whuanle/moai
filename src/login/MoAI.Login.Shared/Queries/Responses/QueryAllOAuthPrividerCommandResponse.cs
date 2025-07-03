// <copyright file="QueryAllOAuthPrividerCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Login.Queries.Responses;

public class QueryAllOAuthPrividerCommandResponse
{
    public List<QueryAllOAuthPrividerCommandResponseItem> Items { get; set; } = new List<QueryAllOAuthPrividerCommandResponseItem>();
}
