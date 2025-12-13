#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Commands;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// <inheritdoc cref="AddWikiDocumentChunkDerivativeCommand"/>
/// </summary>
public class AddWikiDocumentChunkDerivativeCommandHandler : IRequestHandler<AddWikiDocumentChunkDerivativeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiDocumentChunkDerivativeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AddWikiDocumentChunkDerivativeCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AddWikiDocumentChunkDerivativeCommand request, CancellationToken cancellationToken)
    {
        // 检查数据库文档是否存在
        var existDocument = await _databaseContext.WikiDocuments
            .AsNoTracking()
            .AnyAsync(d => d.WikiId == request.WikiId && d.Id == request.DocumentId, cancellationToken);

        if (!existDocument)
        {
            throw new BusinessException("文档不存在");
        }

        List<WikiDocumentChunkDerivativePreviewEntity> derivativePreviewEntities = new();
        foreach (var item in request.Derivatives)
        {
            derivativePreviewEntities.Add(new WikiDocumentChunkDerivativePreviewEntity
            {
                WikiId = request.WikiId,
                DocumentId = request.DocumentId,
                SliceId = item.ChunkId,
                DerivativeType = (int)item.DerivativeType,
                DerivativeContent = item.DerivativeContent
            });
        }

        if (derivativePreviewEntities.Count > 0)
        {
            await _databaseContext.WikiDocumentChunkDerivativePreviews.AddRangeAsync(derivativePreviewEntities, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}