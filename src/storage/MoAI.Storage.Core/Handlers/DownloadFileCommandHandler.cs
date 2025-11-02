using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// <inheritdoc cref="DownloadFileCommand"/>
/// </summary>
public class DownloadFileCommandHandler : IRequestHandler<DownloadFileCommand, EmptyCommandResponse>
{
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadFileCommandHandler"/> class.
    /// </summary>
    /// <param name="storage"></param>
    public DownloadFileCommandHandler(IStorage storage)
    {
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DownloadFileCommand request, CancellationToken cancellationToken)
    {
        await _storage.DownloadAsync(request.ObjectKey, request.LocalFilePath);

        return EmptyCommandResponse.Default;
    }
}