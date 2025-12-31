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
/// <inheritdoc cref="ReadFileStreamCommand"/>
/// </summary>
public class ReadFileStreamCommandHandler : IRequestHandler<ReadFileStreamCommand, ReadFileStreamCommandResponse>
{
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadFileStreamCommandHandler"/> class.
    /// </summary>
    /// <param name="storage"></param>
    public ReadFileStreamCommandHandler(IStorage storage)
    {
        _storage = storage;
    }

    public async Task<ReadFileStreamCommandResponse> Handle(ReadFileStreamCommand request, CancellationToken cancellationToken)
    {
        var tempFilePath = System.IO.Path.GetTempPath();

        var objectKey = request.ObjectKey.TrimStart('/', '\\');
        var tempFileFullPath = System.IO.Path.Combine(tempFilePath, objectKey);

        var fileInfo = new FileInfo(tempFileFullPath);

        if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
        {
            fileInfo.Directory.Create();
        }

        await _storage.DownloadAsync(request.ObjectKey, tempFileFullPath);

        var stream = File.OpenRead(tempFileFullPath);

        return new ReadFileStreamCommandResponse
        {
            FileStream = stream
        };
    }
}