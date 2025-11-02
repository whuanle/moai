using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;
using MoAI.Storage.Services;
using MoAI.Store.Services;
using System.Net;

namespace MoAI.Storage.Handlers;

/// <summary>
/// 预上传文件.
/// </summary>
public class PreuploadFileCommandHandler : IRequestHandler<PreUploadFileCommand, PreUploadFileCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreuploadFileCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    /// <param name="storage"></param>
    public PreuploadFileCommandHandler(DatabaseContext dbContext, IMediator mediator, SystemOptions systemOptions, IStorage storage)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<PreUploadFileCommandResponse> Handle(PreUploadFileCommand request, CancellationToken cancellationToken)
    {
        // 如果文件的 md5 已存在并且文件大小相同，则直接返回文件的 oss 地址，无需重复上传
        // public 和 private 不可以是同一个桶
        var file = await _dbContext.Files.FirstOrDefaultAsync(x => x.ObjectKey == request.ObjectKey, cancellationToken);

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
                FileExtension = Path.GetExtension(request.ObjectKey) ?? string.Empty,
                FileMd5 = request.MD5,
                FileSize = request.FileSize,
                ContentType = request.ContentType,
                IsUploaded = false,
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

        var uploadUrl = await _storage.GeneratePreSignedUploadUrlAsync(new FileObject
        {
            ObjectKey = fileEntity.ObjectKey,
            MaxFileSize = request.FileSize,
            ContentType = request.ContentType,
            ExpiryDuration = request.Expiration
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