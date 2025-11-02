using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Services;

namespace MoAI.Store.Commands;

/// <summary>
/// 完成文件上传.
/// </summary>
public class ComplateFileCommandHandler : IRequestHandler<ComplateFileUploadCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly UserContext _userContext;
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateFileCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="userContext"></param>
    /// <param name="storage"></param>
    public ComplateFileCommandHandler(DatabaseContext dbContext, UserContext userContext, IStorage storage)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ComplateFileUploadCommand request, CancellationToken cancellationToken)
    {
        var fileEntity = await _dbContext.Files.FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);

        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        // 文件早已上传完毕，忽略用户请求
        if (fileEntity.IsUploaded)
        {
            return EmptyCommandResponse.Default;
        }

        // 检查该文件是否当前用户上传的，否则无法完成上传
        if (fileEntity.UpdateUserId != _userContext.UserId)
        {
            throw new BusinessException("其他用户正在上传此文件") { StatusCode = 409 };
        }

        var existFile = await _storage.FileExistsAsync(fileEntity.ObjectKey);
        var fileLength = await _storage.GetFileSizeAsync(fileEntity.ObjectKey);

        // 如果文件已存在且上传失败，则删除该文件
        if (!request.IsSuccess)
        {
            if (existFile)
            {
                await _storage.DeleteFilesAsync(new[] { fileEntity.ObjectKey });
            }

            _dbContext.Files.Remove(fileEntity);
            await _dbContext.SaveChangesAsync();

            return EmptyCommandResponse.Default;
        }

        if (!existFile || fileEntity.FileSize != fileLength)
        {
            if (existFile)
            {
                await _storage.DeleteFilesAsync(new[] { fileEntity.ObjectKey });
            }

            _dbContext.Files.Remove(fileEntity);
            await _dbContext.SaveChangesAsync();

            throw new BusinessException("上传的文件已损坏") { StatusCode = 409 };
        }

        fileEntity.IsUploaded = true;
        _dbContext.Update(fileEntity);
        await _dbContext.SaveChangesAsync();

        return EmptyCommandResponse.Default;
    }
}
