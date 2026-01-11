#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Commands;
using System.Transactions;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// <inheritdoc cref="AddWikiDocumentChunksCommand"/>
/// </summary>
public class AddWikiDocumentChunksCommandHandler : IRequestHandler<AddWikiDocumentChunksCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiDocumentChunksCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AddWikiDocumentChunksCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AddWikiDocumentChunksCommand request, CancellationToken cancellationToken)
    {
        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var chunkEntity = new WikiDocumentChunkContentPreviewEntity
        {
            WikiId = request.WikiId,
            DocumentId = request.DocumentId,
            SliceContent = request.Text,
            SliceLength = request.Text.Length,
            SliceOrder = request.Order
        };

        await _databaseContext.WikiDocumentChunkContentPreviews.AddAsync(chunkEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        List<WikiDocumentChunkMetadataPreviewEntity> metadataPreviewEntities = new();
        if (request.Metadatas != null && request.Metadatas.Count > 0)
        {
            foreach (var item in request.Metadatas)
            {
                metadataPreviewEntities.Add(new WikiDocumentChunkMetadataPreviewEntity
                {
                    WikiId = request.WikiId,
                    DocumentId = request.DocumentId,
                    ChunkId = chunkEntity.Id,
                    MetadataType = (int)item.MetadataType,
                    MetadataContent = item.MetadataContent
                });
            }

            await _databaseContext.WikiDocumentChunkMetadataPreviews.AddRangeAsync(metadataPreviewEntities, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}