// <copyright file="DeleteFileCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Store.Enums;

namespace MoAI.Storage.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteFileCommand"/>
/// </summary>
public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteFileCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    public DeleteFileCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        var fileEntity = await _databaseContext.Files
            .Where(x => x.Id == request.FileId)
            .FirstOrDefaultAsync(cancellationToken);

        if (fileEntity != null)
        {
            _databaseContext.Files.Remove(fileEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);

            var visibility = (fileEntity.IsPublic ? FileVisibility.Public : FileVisibility.Private).ToString().ToLower();
            var filePath = Path.Combine(_systemOptions.FilePath, visibility, fileEntity.ObjectKey);

            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }
        }

        return EmptyCommandResponse.Default;
    }
}