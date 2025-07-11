// <copyright file="UpdateAiModelEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel;
using MoAI.AiModel.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Common.Queries;

namespace MaomiAI.AiModel.Api.Endpoints;

/// <summary>
/// 修改 AI 模型信息，key 要使用 RSA 公钥加密，如果不修改 key 需设置 key=*.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update")]
public class UpdateAiModelEndpoint : Endpoint<UpdateAiModelCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAiModelEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateAiModelEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateAiModelCommand req, CancellationToken ct)
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