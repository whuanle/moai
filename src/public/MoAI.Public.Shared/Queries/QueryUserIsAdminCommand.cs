// <copyright file="QueryUserIsAdminCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Public.Queries.Response;

namespace MoAI.Public.Queries;

public class QueryUserIsAdminCommand : IRequest<QueryUserIsAdminCommandResponse>
{
    public int UserId { get; init; }
}
