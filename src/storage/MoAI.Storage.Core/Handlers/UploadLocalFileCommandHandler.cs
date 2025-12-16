using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <inheritdoc cref="UploadFileStreamCommand"/>
public class UploadLocalFileCommandHandler : IRequestHandler<UploadFileStreamCommand, FileUploadResult>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadLocalFileCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="storage"></param>
    public UploadLocalFileCommandHandler(DatabaseContext databaseContext, IStorage storage)
    {
        _databaseContext = databaseContext;
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<FileUploadResult> Handle(UploadFileStreamCommand request, CancellationToken cancellationToken)
    {
        // 如果文件的 md5 已存在并且文件大小相同，则直接返回文件的 oss 地址，无需重复上传
        // public 和 private 不可以是同一个桶
        var fileEntity = await _databaseContext.Files.FirstOrDefaultAsync(x => x.ObjectKey == request.ObjectKey, cancellationToken);

        // 文件已存在，直接复用
        if (fileEntity != null && fileEntity.IsUploaded)
        {
            return new FileUploadResult
            {
                FileId = fileEntity.Id,
                ObjectKey = fileEntity.ObjectKey,
                FileMd5 = fileEntity.FileMd5,
                FileType = fileEntity.ContentType,
            };
        }

        await _storage.UploadFileAsync(request.FileStream, request.ObjectKey);

        if (fileEntity == null)
        {
            fileEntity = new FileEntity
            {
                FileExtension = Path.GetExtension(request.ObjectKey) ?? string.Empty,
                FileMd5 = request.MD5,
                FileSize = request.FileSize,
                ContentType = request.ContentType,
                IsUploaded = true,
                ObjectKey = request.ObjectKey
            };

            await _databaseContext.Files.AddAsync(fileEntity, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // 已存在预上传记录，但是还没有上传，复用相同的 id，
            // 不过更新人会变化
            fileEntity.UpdateTime = DateTimeOffset.Now;
            fileEntity.IsUploaded = true;
            _databaseContext.Files.Update(fileEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return new FileUploadResult
        {
            FileId = fileEntity.Id,
            ObjectKey = fileEntity.ObjectKey,
            FileMd5 = fileEntity.FileMd5,
            FileType = fileEntity.ContentType
        };
    }
}
