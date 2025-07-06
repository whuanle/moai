// <copyright file="PreUploadImageendpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Routing;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;

namespace MoAI.Storage.Endpoints;

/// <summary>
/// 获取预上传文件签名地址，只能用于上传公开类文件，如头像等.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/pre_upload_image")]
public class PreUploadImageendpoint : Endpoint<PreUploadImageCommand, PreUploadFileCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadImageendpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public PreUploadImageendpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override Task<PreUploadFileCommandResponse> ExecuteAsync(PreUploadImageCommand req, CancellationToken ct)
        => _mediator.Send(req, ct);
}