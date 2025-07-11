// <copyright file="PreUploadOpenApiFileEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MaomiAI.Plugin.Shared.Commands.Responses;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Public.Queries;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 预上传 openapi 文件.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/pre_upload_openapi")]
public class PreUploadOpenApiFileEndpoint : Endpoint<PreUploadOpenApiFilePluginCommand, PreUploadOpenApiFilePluginCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadOpenApiFileEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public PreUploadOpenApiFileEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<PreUploadOpenApiFilePluginCommandResponse> ExecuteAsync(PreUploadOpenApiFilePluginCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            UserId = _userContext.UserId,
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}
