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
/// 使用 AI 优化提示词.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/ai_optmize_prompt")]
public class AiOptimizePromptEndpoint : Endpoint<AiOptimizePromptRequest, QueryAiOptimizePromptCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiOptimizePromptEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AiOptimizePromptEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryAiOptimizePromptCommandResponse> ExecuteAsync(AiOptimizePromptRequest req, CancellationToken ct)
    {
        var newReq = new AiOptimizePromptCommand
        {
            AiModelId = req.AiModelId,
            SourcePrompt = req.SourcePrompt,
            UserId = _userContext.UserId
        };

        return await _mediator.Send(newReq, ct);
    }
}