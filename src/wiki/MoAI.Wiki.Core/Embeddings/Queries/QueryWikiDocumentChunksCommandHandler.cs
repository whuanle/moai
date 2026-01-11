using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.Chunkers;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// 查询知识库文档被切割的块.
/// </summary>
public class QueryWikiDocumentChunksCommandHandler : IRequestHandler<QueryWikiDocumentChunksCommand, QueryWikiDocumentChunksCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentChunksCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiDocumentChunksCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentChunksCommandResponse> Handle(QueryWikiDocumentChunksCommand request, CancellationToken cancellationToken)
    {
        List<WikiDocumenChunkItem> chunks = new();

        var documentEntity = await _databaseContext.WikiDocuments.FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.Id == request.DocumentId, cancellationToken);
        if (documentEntity == null)
        {
            throw new BusinessException("未找到文档");
        }

        var partitionItems = await _databaseContext.WikiDocumentChunkContentPreviews.Where(x => x.WikiId == x.WikiId && x.DocumentId == request.DocumentId).ToArrayAsync();
        var sliceIds = partitionItems.Select(x => x.Id).ToArray();
        var metadataItems = await _databaseContext.WikiDocumentChunkMetadataPreviews.Where(x => x.DocumentId == request.DocumentId && sliceIds.Contains(x.ChunkId)).ToArrayAsync();

        foreach (var item in partitionItems)
        {
            var metadatas = metadataItems.Where(x => x.ChunkId == item.Id).Select(x => new WikiDocumentMetadataItem
            {
                MetadataType = (ParagrahProcessorMetadataType)x.MetadataType,
                MetadataContent = x.MetadataContent
            }).ToArray();

            chunks.Add(new WikiDocumenChunkItem
            {
                Order = item.SliceOrder,
                Text = item.SliceContent,
                ChunkId = item.Id,
                Metadatas = metadatas
            });
        }

        return new QueryWikiDocumentChunksCommandResponse
        {
            Items = chunks.OrderBy(x => x.Order).ToArray()
        };
    }
}
