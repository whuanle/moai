// <copyright file="UpdateWikiConfigCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// 更新知识库配置.
/// </summary>
public class UpdateWikiConfigCommandHandler : IRequestHandler<UpdateWikiConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public UpdateWikiConfigCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiConfigCommand request, CancellationToken cancellationToken)
    {
        var wikiEntity = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).FirstOrDefaultAsync();

        if (wikiEntity == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 500 };
        }

        if (wikiEntity.IsLock)
        {
            throw new BusinessException("知识库已进行锁定，禁止改动配置") { StatusCode = 409 };
        }

        wikiEntity.EmbeddingDimensions = request.EmbeddingDimensions;
        wikiEntity.EmbeddingModelId = request.EmbeddingModelId;
        wikiEntity.EmbeddingModelTokenizer = request.EmbeddingModelTokenizer.ToJsonString();
        wikiEntity.EmbeddingBatchSize = request.EmbeddingBatchSize;
        wikiEntity.MaxRetries = request.MaxRetries;

        _databaseContext.Update(wikiEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
