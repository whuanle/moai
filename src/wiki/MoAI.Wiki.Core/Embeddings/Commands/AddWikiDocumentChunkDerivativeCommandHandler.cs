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
/// <inheritdoc cref="AddWikiDocumentChunkMetadataCommand"/>
/// </summary>
public class AddWikiDocumentChunkMetadataCommandHandler : IRequestHandler<AddWikiDocumentChunkMetadataCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiDocumentChunkMetadataCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AddWikiDocumentChunkMetadataCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AddWikiDocumentChunkMetadataCommand request, CancellationToken cancellationToken)
    {
        // 检查数据库文档是否存在
        var existDocument = await _databaseContext.WikiDocuments
            .AsNoTracking()
            .AnyAsync(d => d.WikiId == request.WikiId && d.Id == request.DocumentId, cancellationToken);

        if (!existDocument)
        {
            throw new BusinessException("文档不存在");
        }

        List<WikiDocumentChunkMetadataPreviewEntity> metadataPreviewEntities = new();
        foreach (var item in request.Metadatas)
        {
            metadataPreviewEntities.Add(new WikiDocumentChunkMetadataPreviewEntity
            {
                WikiId = request.WikiId,
                DocumentId = request.DocumentId,
                ChunkId = item.ChunkId,
                MetadataType = (int)item.MetadataType,
                MetadataContent = item.MetadataContent
            });
        }

        if (metadataPreviewEntities.Count > 0)
        {
            await _databaseContext.WikiDocumentChunkMetadataPreviews.AddRangeAsync(metadataPreviewEntities, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}