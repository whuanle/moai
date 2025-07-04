// <copyright file="QueryUserIsAdminCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;

namespace MoAI.Public.Queries.Response;

public class QueryUserIsAdminCommandResponse
{
    public bool IsAdmin { get; init; }
    public bool IsRoot { get; init; }
}
