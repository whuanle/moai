// <copyright file="QueryPrompListEndpoints.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MaomiAI.Prompt.Api;
using MaomiAI.Prompt.Queries;
using MaomiAI.Prompt.Queries.Responses;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.PromptEndpoints;

/// <summary>
/// 查询提示词.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/prompt_list")]
public class QueryPrompListEndpoints : Endpoint<QueryPromptListCommand, QueryPromptListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPrompListEndpoints"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPrompListEndpoints(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPromptListCommandResponse> ExecuteAsync(QueryPromptListCommand req, CancellationToken ct)
    {
        var newReq = new QueryPromptListCommand
        {
            ClassId = req.ClassId,
            Name = req.Name,
            UserId = _userContext.UserId,
            IsOwn = req.IsOwn
        };
        return await _mediator.Send(newReq, ct);
    }
}
