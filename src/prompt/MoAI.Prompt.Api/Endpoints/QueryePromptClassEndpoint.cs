// <copyright file="QueryPrompListtEndpoints.cs" company="MaomiAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

using FastEndpoints;
using MaomiAI.Prompt.Api;
using MaomiAI.Prompt.Queries;
using MaomiAI.Prompt.Queries.Responses;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Prompt.Queries;
using MoAI.Prompt.Queries.Responses;

namespace MaomiAI.AiModel.Api.Endpoints;

/// <summary>
/// 查询分类.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/class_list")]
public class QueryePromptClassEndpoint : Endpoint<QueryePromptClassCommand, QueryePromptClassCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryePromptClassEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryePromptClassEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    public override async Task<QueryePromptClassCommandResponse> ExecuteAsync(QueryePromptClassCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req);
    }
}