#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.Embedding.Commands;
using System.Transactions;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// <inheritdoc cref="UpdateWikiDocumentChunksCommand"/>
/// </summary>
public class UpdateWikiDocumentChunksCommandHandler : IRequestHandler<UpdateWikiDocumentChunksCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiDocumentChunksCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWikiDocumentChunksCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiDocumentChunksCommand request, CancellationToken cancellationToken)
    {
        var chunkIds = request.Chunks.Select(x => x.ChunkId).ToArray();

        var chunks = await _databaseContext.WikiDocumentChunkContentPreviews
            .Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId && chunkIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        if (chunks.Length == 0)
        {
            return EmptyCommandResponse.Default;
        }

        List<WikiDocumentChunkMetadataPreviewEntity> metadataPreviewEntities = new();

        foreach (var item in request.Chunks)
        {
            var entity = chunks.FirstOrDefault(x => x.Id == item.ChunkId);
            if (entity == null)
            {
                continue;
            }

            entity.SliceContent = item.Text;
            entity.SliceLength = item.Text.Length;
            entity.SliceOrder = item.Order;

            if (item.Metadatas != null && item.Metadatas.Count > 0)
            {
                foreach (var metadata in item.Metadatas)
                {
                    metadataPreviewEntities.Add(new WikiDocumentChunkMetadataPreviewEntity
                    {
                        WikiId = request.WikiId,
                        DocumentId = request.DocumentId,
                        ChunkId = entity.Id,
                        MetadataType = (int)metadata.MetadataType,
                        MetadataContent = metadata.MetadataContent
                    });
                }
            }
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        await _databaseContext.WikiDocumentChunkMetadataPreviews
            .Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId && chunkIds.Contains(x.ChunkId))
            .ExecuteDeleteAsync();

        if (metadataPreviewEntities.Count > 0)
        {
            await _databaseContext.WikiDocumentChunkMetadataPreviews.AddRangeAsync(metadataPreviewEntities, cancellationToken);
        }

        _databaseContext.WikiDocumentChunkContentPreviews.UpdateRange(chunks);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
