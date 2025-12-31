using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Models;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// <inheritdoc cref="DownloadFileCommand"/>
/// </summary>
public class DownloadFileCommandHandler : IRequestHandler<DownloadFileCommand, DownloadFileCommandResponse>
{
    private readonly IStorage _storage;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadFileCommandHandler"/> class.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="databaseContext"></param>
    public DownloadFileCommandHandler(IStorage storage, DatabaseContext databaseContext)
    {
        _storage = storage;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<DownloadFileCommandResponse> Handle(DownloadFileCommand request, CancellationToken cancellationToken)
    {
        var fileRecord = await _databaseContext.Files.FirstOrDefaultAsync(x => x.Id == request.FileId && x.IsUploaded);
        if (fileRecord == null)
        {
            throw new BusinessException("未找到文件");
        }

        var tempFilePath = System.IO.Path.GetTempPath();

        var objectKey = fileRecord.ObjectKey.TrimStart('/', '\\');
        var tempFileFullPath = System.IO.Path.Combine(tempFilePath, objectKey);

        var response = new DownloadFileCommandResponse
        {
            LocalFilePath = tempFileFullPath,
            ContentType = fileRecord.ContentType,
            FileExtension = fileRecord.FileExtension,
            FileMd5 = fileRecord.FileMd5,
            FileSize = fileRecord.FileSize,
            ObjectKey = fileRecord.ObjectKey
        };

        var fileInfo = new FileInfo(tempFileFullPath);
        if (fileInfo.Exists)
        {
            if (fileInfo.Length == fileRecord.FileSize)
            {
                return response;
            }

            fileInfo.Delete();
        }

        if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
        {
            fileInfo.Directory.Create();
        }

        await _storage.DownloadAsync(fileRecord.ObjectKey, tempFileFullPath);
        return response;
    }
}
