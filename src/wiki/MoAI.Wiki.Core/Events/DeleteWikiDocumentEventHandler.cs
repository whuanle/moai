// <copyright file="DeleteWikiDocumentEventHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Database;

namespace MoAI.Wiki.Events;

/// <summary>
/// <inheritdoc cref="DeleteWikiDocumentEvent"/>
/// </summary>
public class DeleteWikiDocumentEventHandler : INotificationHandler<DeleteWikiDocumentEvent>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentEventHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteWikiDocumentEventHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task Handle(DeleteWikiDocumentEvent notification, CancellationToken cancellationToken)
    {
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiWebDocuments.Where(x => x.WikiDocumentId == notification.DocumentId));
    }
}
