// <copyright file="QueryePromptClassEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MaomiAI.Prompt.Api;
using MaomiAI.Prompt.Models;
using MaomiAI.Prompt.Queries;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Prompt.Queries;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.PromptClassEndpoints;

/// <summary>
/// 查询分类.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/class_list")]
public class QueryPromptClassEndpoint : EndpointWithoutRequest<QueryePromptClassCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptClassEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPromptClassEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryePromptClassCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(new QueryePromptClassCommand());
    }
}
