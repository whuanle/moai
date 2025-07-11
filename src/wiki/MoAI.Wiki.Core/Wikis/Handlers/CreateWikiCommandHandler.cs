// <copyright file="CreateWikiCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// 创建知识库.
/// </summary>
public class CreateWikiCommandHandler : IRequestHandler<CreateWikiCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateWikiCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateWikiCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.Wikis.AnyAsync(x => x.Name == request.Name, cancellationToken);
        if (exist)
        {
            throw new BusinessException("已存在同名知识库") { StatusCode = 409 };
        }

        var wikiEntity = new WikiEntity
        {
            Name = request.Name,
            Description = request.Description,
            EmbeddingModelId = default,
            EmbeddingDimensions = 512,
            EmbeddingModelTokenizer = string.Empty,
            EmbeddingBatchSize = 50,
            MaxRetries = 3,
            IsPublic = false,
            IsLock = false
        };

        await _databaseContext.Wikis.AddAsync(wikiEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt
        {
            Value = wikiEntity.Id
        };
    }
}
