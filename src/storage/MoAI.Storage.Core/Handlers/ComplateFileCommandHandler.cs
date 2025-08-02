// <copyright file="ComplateFileCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Store.Enums;

namespace MoAI.Store.Commands;

/// <summary>
/// 完成文件上传.
/// </summary>
public class ComplateFileCommandHandler : IRequestHandler<ComplateFileUploadCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly UserContext _userContext;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateFileCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="userContext"></param>
    /// <param name="systemOptions"></param>
    public ComplateFileCommandHandler(DatabaseContext dbContext, UserContext userContext, SystemOptions systemOptions)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ComplateFileUploadCommand request, CancellationToken cancellationToken)
    {
        var file = await _dbContext.Files.FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);

        if (file == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        // 文件早已上传完毕，忽略用户请求
        if (file.IsUploaded)
        {
            return EmptyCommandResponse.Default;
        }

        // 检查该文件是否当前用户上传的，否则无法完成上传
        if (file.UpdateUserId != _userContext.UserId)
        {
            throw new BusinessException("文件不属于当前用户上传") { StatusCode = 403 };
        }

        var visibility = (file.IsPublic ? FileVisibility.Public : FileVisibility.Private).ToString().ToLower();

        var filePath = Path.Combine(_systemOptions.FilePath, visibility, file.ObjectKey);
        var fileInfo = new FileInfo(filePath);

        // 如果文件已存在且上传失败，则删除该文件
        if (!request.IsSuccess)
        {
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }

            _dbContext.Files.Remove(file);
            await _dbContext.SaveChangesAsync();

            return EmptyCommandResponse.Default;
        }

        if (!fileInfo.Exists || file.FileSize != fileInfo.Length)
        {
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }

            _dbContext.Files.Remove(file);
            await _dbContext.SaveChangesAsync();

            throw new BusinessException("上传的文件已损坏") { StatusCode = 400 };
        }

        file.IsUploaded = true;
        _dbContext.Update(file);
        await _dbContext.SaveChangesAsync();

        return EmptyCommandResponse.Default;
    }
}
