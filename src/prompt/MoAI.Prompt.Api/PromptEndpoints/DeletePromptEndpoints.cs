// <copyright file="DeletePromptEndpoints.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MaomiAI.Prompt.Api;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Prompt.Queries;

namespace MoAI.Prompt.PromptEndpoints;

/// <summary>
/// 删除提示词.
/// </summary>
[HttpDelete($"{ApiPrefix.Prefix}/delete_prompt")]
public class DeletePromptEndpoints : Endpoint<DeletePromptCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptEndpoints"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DeletePromptEndpoints(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeletePromptCommand req, CancellationToken ct)
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