// <copyright file="CreatePromptClassEndpoints.cs" company="MoAI">
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
using MoAI.Public.Queries;

namespace MaomiAI.AiModel.Api.Endpoints;

/// <summary>
/// 创建提示词分类.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/create_class")]
public class CreatePromptClassEndpoints : Endpoint<CreatePromptClassCommand, SimpleInt>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptClassEndpoints"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public CreatePromptClassEndpoints(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(CreatePromptClassCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            UserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
