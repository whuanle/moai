// <copyright file="CreateWikiEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 创建知识库.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/create")]
public class CreateWikiEndpoint : Endpoint<CreateWikiCommand, SimpleInt>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public CreateWikiEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(CreateWikiCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req);
    }
}
