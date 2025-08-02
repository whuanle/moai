// <copyright file="UpdatePromptEndpoints.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MoAIPrompt.Api;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Prompt.Queries;

namespace MoAI.Prompt.PromptEndpoints;

/// <summary>
/// 修改提示词.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_prompt")]
public class UpdatePromptEndpoints : Endpoint<UpdatePromptCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptEndpoints"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdatePromptEndpoints(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdatePromptCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryPromptCreateUserCommand
        {
            PromptId = req.PromptId
        });

        if (isCreator.UserId != _userContext.UserId)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
