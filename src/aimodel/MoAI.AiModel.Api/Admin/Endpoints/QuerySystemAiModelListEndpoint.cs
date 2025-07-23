// <copyright file="QuerySystemAiModelListEndpoint.cs" company="MoAI">
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

namespace MoAI.AiModel.Admin.Endpoints;

/// <summary>
/// 获取 AI 模型列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/type/system_modellist")]
public class QuerySystemAiModelListEndpoint : Endpoint<QuerySystemAiModelListCommand, QuerySystemAiModelListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemAiModelListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QuerySystemAiModelListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QuerySystemAiModelListCommandResponse> ExecuteAsync(QuerySystemAiModelListCommand req, CancellationToken ct)
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
