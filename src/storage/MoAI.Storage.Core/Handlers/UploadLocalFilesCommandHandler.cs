// <copyright file="UploadLocalFilesCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Storage.Commands;
using MoAI.Store.Enums;
using MoAI.Store.Services;

namespace MoAI.Store.Handlers;

public class UploadLocalFilesCommandHandler : IRequestHandler<UploadLocalFilesCommand, UploadLocalFilesCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;

    public UploadLocalFilesCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<UploadLocalFilesCommandResponse> Handle(UploadLocalFilesCommand request, CancellationToken cancellationToken)
    {
        List<FileEntity> fileEntities = new List<FileEntity>();
        var uploadedFiles = new List<FileUploadResult>();

        IFileStorage fileStorage = request.Visibility == FileVisibility.Public ? _serviceProvider.GetRequiredService<IPublicFileStorage>() : _serviceProvider.GetRequiredService<IPrivateFileStorage>();

        foreach (var item in request.Files)
        {
            using var fileStream = new FileStream(item.FilePath, FileMode.Open);
            await fileStorage.UploadFileAsync(fileStream, item.ObjectKey);

            fileEntities.Add(new FileEntity
            {
                FileName = item.FileName,
                ObjectKey = item.ObjectKey,
                FileMd5 = item.MD5,
                FileSize = (int)fileStream.Length,
                IsPublic = request.Visibility == FileVisibility.Public,
                IsUploaded = true,
                ContentType = item.ContentType,
            });
        }

        await _databaseContext.Files.AddRangeAsync(fileEntities, cancellationToken: cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        foreach (var item in fileEntities)
        {
            uploadedFiles.Add(new FileUploadResult
            {
                FileId = item.Id,
                FileName = item.FileName,
                ObjectKey = item.ObjectKey,
            });
        }

        return new UploadLocalFilesCommandResponse
        {
            Files = uploadedFiles
        };
    }
}
