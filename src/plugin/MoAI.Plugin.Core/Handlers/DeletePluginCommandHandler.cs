﻿// <copyright file="DeletePluginCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Storage.Commands;
using System.Transactions;

namespace MoAI.Plugin.Handlers;

/// <summary>
/// DeletePluginCommand.
/// </summary>
public class DeletePluginCommandHandler : IRequestHandler<DeletePluginCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public DeletePluginCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePluginCommand request, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);
        if (pluginEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        await _mediator.Send(new DeleteFileCommand
        {
            FileId = pluginEntity.OpenapiFileId,
        });

        _databaseContext.Plugins.Remove(pluginEntity);

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginId == request.PluginId));
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
