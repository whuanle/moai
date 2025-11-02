using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteFileCommand"/>
/// </summary>
public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteFileCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="storage"></param>
    public DeleteFileCommandHandler(DatabaseContext databaseContext, IStorage storage)
    {
        _databaseContext = databaseContext;
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        if (request.FileIds.Count == 0)
        {
            return EmptyCommandResponse.Default;
        }

        var fileEntities = await _databaseContext.Files
            .Where(x => request.FileIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        _databaseContext.Files.RemoveRange(fileEntities);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var objectKeys = fileEntities.Select(x => x.ObjectKey).ToList();

        await _storage.DeleteFilesAsync(objectKeys);
        return EmptyCommandResponse.Default;
    }
}