// <copyright file="PreloadWikiDocumentEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Documents.Commands;
using MoAI.Wiki.Documents.Commands.Responses;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Documents.Endpoints;

/// <summary>
/// 预上传文档.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/document/preload_document")]
public class PreloadWikiDocumentEndpoint : Endpoint<PreUploadWikiDocumentCommand, PreloadWikiDocumentResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreloadWikiDocumentEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public PreloadWikiDocumentEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<PreloadWikiDocumentResponse> ExecuteAsync(PreUploadWikiDocumentCommand req, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            UserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
