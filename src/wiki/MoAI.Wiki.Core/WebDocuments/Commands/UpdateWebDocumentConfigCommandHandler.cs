// <copyright file="UpdateWebDocumentConfigCommandHandler.cs" company="MoAI">
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

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// <inheritdoc cref="AddWebDocumentConfigCommand"/>
/// </summary>
public class UpdateWebDocumentConfigCommandHandler : IRequestHandler<UpdateWebDocumentConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWebDocumentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWebDocumentConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWebDocumentConfigCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.WikiWebConfigs.FirstOrDefaultAsync(x => x.Id == request.WebConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("网页爬取配置不存在") { StatusCode = 404 };
        }

        entity.IsCrawlOther = request.IsCrawlOther;
        entity.Address = request.Address;
        entity.Title = request.Title.Trim();


        _databaseContext.WikiWebConfigs.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
