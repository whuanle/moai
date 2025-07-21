// <copyright file="DeleteWebDocumentConfigCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// <inheritdoc cref="DeleteWebDocumentConfigCommand"/>
/// </summary>
public class DeleteWebDocumentConfigCommandHandler : IRequestHandler<DeleteWebDocumentConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWebDocumentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteWebDocumentConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWebDocumentConfigCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
