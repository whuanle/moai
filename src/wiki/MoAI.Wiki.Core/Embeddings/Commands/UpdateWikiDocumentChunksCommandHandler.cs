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

        List<WikiDocumentChunkDerivativePreviewEntity> derivativePreviewEntities = new();

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

            foreach (var derivative in item.Derivatives)
            {
                derivativePreviewEntities.Add(new WikiDocumentChunkDerivativePreviewEntity
                {
                    WikiId = request.WikiId,
                    DocumentId = request.DocumentId,
                    SliceId = entity.Id,
                    DerivativeType = (int)derivative.DerivativeType,
                    DerivativeContent = derivative.DerivativeContent
                });
            }
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        await _databaseContext.WikiDocumentChunkDerivativePreviews
            .Where(x => x.WikiId == request.WikiId && x.DocumentId == request.DocumentId && chunkIds.Contains(x.SliceId))
            .ExecuteDeleteAsync();

        await _databaseContext.WikiDocumentChunkDerivativePreviews.AddRangeAsync(derivativePreviewEntities, cancellationToken);

        _databaseContext.WikiDocumentChunkContentPreviews.UpdateRange(chunks);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}