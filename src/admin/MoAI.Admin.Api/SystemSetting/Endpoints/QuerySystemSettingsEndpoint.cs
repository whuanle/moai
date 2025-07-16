// <copyright file="QuerySystemSettingsEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin.SystemSettings.Queries;
using MoAI.Admin.SystemSettings.Queries.Responses;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Admin.SystemSetting.Endpoints;

/// <summary>
/// 查询系统配置.
/// </summary>
[HttpGet($"{ApiPrefix.Settings}")]
public class QuerySystemSettingsEndpoint : EndpointWithoutRequest<QuerySystemSettingsCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemSettingsEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QuerySystemSettingsEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QuerySystemSettingsCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId });

        if (!isAdmin.IsRoot)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(new QuerySystemSettingsCommand(), ct);
    }
}
