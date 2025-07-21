// <copyright file="DeleteAiModelEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel.Commands;
using MoAI.AiModel.Queries;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Endpoints;

/// <summary>
/// 删除一个模型.
/// </summary>
[HttpDelete($"{ApiPrefix.Prefix}/delete_model")]
public class DeleteAiModelEndpoint : Endpoint<DeleteAiModelCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiModelEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DeleteAiModelEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeleteAiModelCommand req, CancellationToken ct)
    {
        var aiModelCreator = await _mediator.Send(new QueryAiModelCreatorCommand
        {
            ModelId = req.AiModelId
        });

        if (!aiModelCreator.Exist)
        {
            throw new BusinessException("未找到模型") { StatusCode = 404 };
        }

        if (aiModelCreator.IsSystem)
        {
            var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
            {
                UserId = _userContext.UserId
            });

            if (!isAdmin.IsAdmin)
            {
                throw new BusinessException("没有操作权限.") { StatusCode = 403 };
            }
        }
        else if (aiModelCreator.CreatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到模型.") { StatusCode = 404 };
        }

        return await _mediator.Send(req);
    }
}