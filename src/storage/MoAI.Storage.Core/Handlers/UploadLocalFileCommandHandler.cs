// <copyright file="UploadLocalFileCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;
using MoAI.Store.Enums;
using System.Net;

namespace MoAI.Storage.Handlers;

public class UploadLocalFileCommandHandler : IRequestHandler<UploadLocalFileCommand, FileUploadResult>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;

    public UploadLocalFileCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    public async Task<FileUploadResult> Handle(UploadLocalFileCommand request, CancellationToken cancellationToken)
    {
        var isPublic = request.Visibility == FileVisibility.Public ? true : false;

        // 如果文件的 md5 已存在并且文件大小相同，则直接返回文件的 oss 地址，无需重复上传
        // public 和 private 不可以是同一个桶
        var file = await _databaseContext.Files.FirstOrDefaultAsync(x => x.IsPublic == isPublic && x.ObjectKey == request.File.ObjectKey, cancellationToken);

        // 文件已存在，直接复用
        if (file != null && file.IsUploaded)
        {
            return new FileUploadResult
            {
                FileId = file.Id,
                FileName = file.FileName,
                ObjectKey = file.ObjectKey,
            };
        }

        var fileInfo = new FileInfo(request.File.FilePath);
        var targetFilePath = Path.Combine(_systemOptions.FilePath, "private", request.File.ObjectKey);
        var targetFileInfo = new FileInfo(targetFilePath);

        if (!fileInfo.Exists)
        {
            throw new BusinessException("文件不存在");
        }

        var dir = Directory.GetParent(targetFilePath)!;
        if (!dir.Exists)
        {
            dir.Create();
        }

        // 复制文件到目标路径
        if (targetFileInfo.Exists)
        {
            targetFileInfo.Delete();
        }

        fileInfo.CopyTo(targetFilePath, true);

        FileEntity fileEntity;
        if (file == null)
        {
            fileEntity = new FileEntity
            {
                FileName = request.File.FileName,
                FileMd5 = request.File.MD5,
                FileSize = (int)fileInfo.Length,
                ContentType = request.File.ContentType,
                IsUploaded = true,
                IsPublic = isPublic,
                ObjectKey = request.File.ObjectKey
            };

            await _databaseContext.Files.AddAsync(fileEntity, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // 已存在预上传记录，但是还没有上传，复用相同的 id，
            // 不过更新人会变化
            fileEntity = file;
            fileEntity.UpdateTime = DateTimeOffset.Now;
            fileEntity.IsUploaded = true;
            _databaseContext.Files.Update(fileEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return new FileUploadResult
        {
            FileId = fileEntity.Id,
            FileName = fileEntity.FileName,
            ObjectKey = fileEntity.ObjectKey,
        };
    }
}
