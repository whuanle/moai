// <copyright file="PreuploadFileCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;
using MoAI.Storage.Helpers;
using MoAI.Store.Enums;
using MoAI.Store.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// 预上传文件.
/// </summary>
public class PreuploadFileCommandHandler : IRequestHandler<PreUploadFileCommand, PreUploadFileCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreuploadFileCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="serviceProvider"></param>
    public PreuploadFileCommandHandler(DatabaseContext dbContext, IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<PreUploadFileCommandResponse> Handle(PreUploadFileCommand request, CancellationToken cancellationToken)
    {
        var isPublic = request.Visibility == FileVisibility.Public ? true : false;

        // 如果文件的 md5 已存在并且文件大小相同，则直接返回文件的 oss 地址，无需重复上传
        // public 和 private 不可以是同一个桶
        var file = await _dbContext.Files.FirstOrDefaultAsync(x => x.IsPublic == isPublic && x.ObjectKey == request.ObjectKey, cancellationToken);

        // 文件已存在，直接复用
        if (file != null && file.IsUploaded)
        {
            return new PreUploadFileCommandResponse
            {
                IsExist = true,
                FileId = file.Id
            };
        }

        FileEntity fileEntity;
        if (file == null)
        {
            fileEntity = new FileEntity
            {
                FileName = request.FileName,
                FileMd5 = request.MD5,
                FileSize = request.FileSize,
                ContentType = request.ContentType,
                IsUploaded = false,
                IsPublic = isPublic,
                ObjectKey = request.ObjectKey
            };

            await _dbContext.Files.AddAsync(fileEntity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // 已存在预上传记录，但是还没有上传，复用相同的 id，
            // 不过更新人会变化
            fileEntity = file;
            fileEntity.UpdateTime = DateTimeOffset.Now;
            _dbContext.Files.Update(fileEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        IFileStorage fileStorage = request.Visibility == FileVisibility.Public ? _serviceProvider.GetRequiredService<IPublicFileStorage>() : _serviceProvider.GetRequiredService<IPrivateFileStorage>();

        // 生成预上传地址
        var uploadUrl = await fileStorage.GeneratePreSignedUploadUrlAsync(new FileObject
        {
            ExpiryDuration = request.Expiration,
            ObjectKey = request.ObjectKey,
            ContentType = request.ContentType,
            MaxFileSize = FileStoreHelper.GetAllowedFileSizeLimit(request.FileSize)
        });

        return new PreUploadFileCommandResponse
        {
            IsExist = false,
            Expiration = DateTimeOffset.Now.Add(request.Expiration),
            FileId = fileEntity.Id,
            UploadUrl = new Uri(uploadUrl)
        };
    }
}