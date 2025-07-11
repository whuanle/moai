// <copyright file="SetSystemSettingsEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin.SystemSettings.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Common.Queries;

namespace MoAI.Admin.SystemSetting.Endpoints;

/// <summary>
/// 更新系统设置.
/// </summary>
[HttpPut($"{ApiPrefix.Settings}")]
public class SetSystemSettingsEndpoint : Endpoint<SetSystemSettingsCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetSystemSettingsEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public SetSystemSettingsEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(SetSystemSettingsCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}