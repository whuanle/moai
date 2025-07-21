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

namespace MoAI.AiModel.QueryEndpoints;

/// <summary>
/// 获取 AI 模型列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/type/user_modellist")]
public class QueryUserAiModelListEndpoint : Endpoint<QueryUserAiModelListCommand, QueryAiModelListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserAiModelListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserAiModelListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryAiModelListCommandResponse> ExecuteAsync(QueryUserAiModelListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req);
    }
}
