// <copyright file="UpdateWikiConfigEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel.Queries;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 更新知识库配置.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_wiki_config")]
public class UpdateWikiConfigEndpoint : Endpoint<UpdateWikiConfigCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiConfigEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateWikiConfigEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateWikiConfigCommand req, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            UserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (!userIsWikiUser.IsWikiRoot)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        var aiModelCreator = await _mediator.Send(new QueryAiModelCreatorCommand
        {
            ModelId = req.EmbeddingModelId
        });

        if (!aiModelCreator.Exist)
        {
            throw new BusinessException("未找到模型") { StatusCode = 404 };
        }

        if (aiModelCreator.IsSystem)
        {
            if (aiModelCreator.IsPublic == false)
            {
                throw new BusinessException("未找到模型.") { StatusCode = 404 };
            }
        }
        else if (aiModelCreator.CreatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到模型.") { StatusCode = 404 };
        }

        return await _mediator.Send(req);
    }
}
