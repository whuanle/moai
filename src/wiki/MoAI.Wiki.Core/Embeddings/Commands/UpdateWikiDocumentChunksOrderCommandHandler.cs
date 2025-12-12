using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Commands;

namespace MoAI.Wiki.Embeddings.Commands;

/// <summary>
/// <inheritdoc cref="UpdateWikiDocumentChunksOrderCommand"/>
/// </summary>
public class UpdateWikiDocumentChunksOrderCommandHandler : IRequestHandler<UpdateWikiDocumentChunksOrderCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiDocumentChunksOrderCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWikiDocumentChunksOrderCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiDocumentChunksOrderCommand request, CancellationToken cancellationToken)
    {
        foreach (var item in request.Chunks)
        {
            await _databaseContext.WikiDocumentChunkContentPreviews.Where(x => x.Id == item.ChunkId)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.SliceOrder, item.Order));
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
