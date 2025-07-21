// <copyright file="UserViewAiModelProviderListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.AiModel.Queries;
using MoAI.AiModel.Queries.Respones;
using MoAI.Infra.Models;

namespace MoAI.AiModel.QueryEndpoints;

/// <summary>
/// 查询ai服务商列表.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/system_providerlist")]
public class QuerySystemAiModelProviderListEndpoint : EndpointWithoutRequest<QueryAiModelProviderListResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemAiModelProviderListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QuerySystemAiModelProviderListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryAiModelProviderListResponse> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(new QuerySystemAiModelProviderListCommand());
    }
}
