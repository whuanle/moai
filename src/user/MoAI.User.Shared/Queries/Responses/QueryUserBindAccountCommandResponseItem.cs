// <copyright file="QueryUserBindAccountCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.User.Queries.Responses;

public class QueryUserBindAccountCommandResponseItem
{
    public int BindId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ProviderId { get; init; }
    public string IconUrl { get; init; } = string.Empty;
}
