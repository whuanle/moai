// <copyright file="AddSystemAiModelEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel.Admin.Models;
using MoAI.AiModel.Commands;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Endpoints;

/// <summary>
/// 添加一个系统 ai 模型，key 要使用 RSA 公钥加密.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/add_system_aimodel")]
public class AddSystemAiModelEndpoint : Endpoint<AddSystemAiModelRequest, SimpleInt>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddSystemAiModelEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AddSystemAiModelEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(AddSystemAiModelRequest req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            UserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        var command = new AddAiModelCommand
        {
            DeploymentName = req.DeploymentName,
            MaxDimension = req.MaxDimension,
            Abilities = req.Abilities,
            AiModelType = req.AiModelType,
            ContextWindowTokens = req.ContextWindowTokens,
            Endpoint = req.Endpoint,
            IsPublic = req.IsPublic,
            IsSystem = true,
            Key = req.Key,
            Name = req.Name,
            Provider = req.Provider,
            TextOutput = req.TextOutput,
            Title = req.Title,
        };

        return await _mediator.Send(command, ct);
    }
}