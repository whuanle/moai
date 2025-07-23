// <copyright file="AddUserAiModelEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel.Commands;
using MoAI.AiModel.User.Models;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Endpoints;

/// <summary>
/// 添加一个用户私有模型配置，key 要使用 RSA 公钥加密.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/add_user_model")]
public class AddUserAiModelEndpoint : Endpoint<AddUserAiModelRequest, SimpleInt>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddUserAiModelEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public AddUserAiModelEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(AddUserAiModelRequest req, CancellationToken ct)
    {
        var command = new AddAiModelCommand
        {
            DeploymentName = req.DeploymentName,
            MaxDimension = req.MaxDimension,
            Abilities = req.Abilities,
            AiModelType = req.AiModelType,
            ContextWindowTokens = req.ContextWindowTokens,
            Endpoint = req.Endpoint,
            IsPublic = false,
            IsSystem = false,
            Key = req.Key,
            Name = req.Name,
            Provider = req.Provider,
            TextOutput = req.TextOutput,
            Title = req.Title,
        };

        return await _mediator.Send(command, ct);
    }
}