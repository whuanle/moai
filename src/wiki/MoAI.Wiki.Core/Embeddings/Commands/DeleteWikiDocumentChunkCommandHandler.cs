using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Commands;
using System.Transactions;

namespace MoAI.Wiki.Embeddings.Commands;

/// <summary>
/// <inheritdoc cref="DeleteWikiDocumentChunkCommand"/>
/// </summary>
public class DeleteWikiDocumentChunkCommandHandler : IRequestHandler<DeleteWikiDocumentChunkCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentChunkCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteWikiDocumentChunkCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiDocumentChunkCommand request, CancellationToken cancellationToken)
    {
        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        await _databaseContext.WikiDocumentChunkContentPreviews
            .Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId && x.Id == request.ChunkId)
            .ExecuteDeleteAsync();

        await _databaseContext.WikiDocumentChunkMetadataPreviews
            .Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId && x.ChunkId == request.ChunkId)
            .ExecuteDeleteAsync();

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
