// <copyright file="QueryWikiConfigInfoCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.WebDocuments.Queries.Responses;

namespace MoAI.Wiki.WebDocuments.Queries;

public class QueryWikiConfigInfoCommand : IRequest<QueryWikiConfigInfoCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int WikiWebConfigId { get; init; }
}
