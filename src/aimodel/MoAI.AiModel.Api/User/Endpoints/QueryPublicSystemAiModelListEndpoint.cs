// <copyright file="QueryUserAiModelListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel.Queries;
using MoAI.AiModel.Queries.Respones;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.User.Endpoints;

/// <summary>
/// 获取开放的系统 AI 模型列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/type/public_system_aimodel_list")]
public class QueryPublicSystemAiModelListEndpoint : Endpoint<QueryPublicSystemAiModelListCommand, QueryPublicSystemAiModelListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicSystemAiModelListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPublicSystemAiModelListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPublicSystemAiModelListCommandResponse> ExecuteAsync(QueryPublicSystemAiModelListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req);
    }
}
