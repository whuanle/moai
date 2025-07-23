// <copyright file="UpdateUserAiModelEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel.Commands;
using MoAI.AiModel.Queries;
using MoAI.AiModel.User.Models;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Endpoints;

/// <summary>
/// 修改 AI 模型信息，key 要使用 RSA 公钥加密，如果不修改 key 需设置 key=*.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_user_aimodel")]
public class UpdateUserAiModelEndpoint : Endpoint<UpdateUserAiModelRequest, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserAiModelEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateUserAiModelEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateUserAiModelRequest req, CancellationToken ct)
    {
        var aiModelCreator = await _mediator.Send(new QueryAiModelCreatorCommand
        {
            ModelId = req.AiModelId
        });

        if (!aiModelCreator.Exist)
        {
            throw new BusinessException("未找到模型") { StatusCode = 404 };
        }

        if (aiModelCreator.CreatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到模型.") { StatusCode = 404 };
        }

        var command = new UpdateAiModelCommand
        {
            AiModelId = req.AiModelId,
            DeploymentName = req.DeploymentName,
            MaxDimension = req.MaxDimension,
            Abilities = req.Abilities,
            AiModelType = req.AiModelType,
            ContextWindowTokens = req.ContextWindowTokens,
            Endpoint = req.Endpoint,
            IsPublic = false,
            Key = req.Key,
            Name = req.Name,
            Provider = req.Provider,
            TextOutput = req.TextOutput,
            Title = req.Title,
        };

        return await _mediator.Send(command, ct);
    }
}