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
using MoAI.Prompt.PromptEndpoints.Models;
using MoAI.Prompt.Queries;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.PromptEndpoints;

/// <summary>
/// 获取提示词内容.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/prompt_content")]
public class QueryPromptEndpoint : Endpoint<QueryPromptContentRequest, PromptItem>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPromptEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<PromptItem> ExecuteAsync(QueryPromptContentRequest req, CancellationToken ct)
    {
        var newReq = new QueryPromptCommand
        {
            PromptId = req.PromptId,
            UserId = _userContext.UserId
        };

        return await _mediator.Send(newReq, ct);
    }
}
