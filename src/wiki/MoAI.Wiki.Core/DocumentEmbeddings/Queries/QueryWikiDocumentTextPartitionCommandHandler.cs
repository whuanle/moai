using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.Chunkers;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// 查询知识库文档被切割的块命令.
/// </summary>
public class QueryWikiDocumentTextPartitionCommandHandler : IRequestHandler<QueryWikiDocumentTextPartitionCommand, QueryWikiDocumentTextPartitionCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentTextPartitionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiDocumentTextPartitionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentTextPartitionCommandResponse> Handle(QueryWikiDocumentTextPartitionCommand request, CancellationToken cancellationToken)
    {
        List<WikiDocumentTextPartitionPreviewItem> chunks = new();

        var documentEntity = await _databaseContext.WikiDocuments.FirstOrDefaultAsync(x => x.Id == request.WikiId && x.Id == request.DocumentId, cancellationToken);
        if (documentEntity == null)
        {
            throw new BusinessException("未找到文档");
        }

        var chunkConfig = documentEntity.SpliceConfig.JsonToObject<PlainTextChunkerOptions>()!;

        var partitionItems = await _databaseContext.WikiDocumentSliceContentPreviews.Where(x => x.WikiId == x.WikiId && x.Id == request.DocumentId).ToArrayAsync();

        foreach (var item in partitionItems)
        {
            chunks.Add(new WikiDocumentTextPartitionPreviewItem
            {
                Order = item.SliceOrder,
                Text = item.SliceContent,
                ChunkId = item.Id
            });
        }

        return new QueryWikiDocumentTextPartitionCommandResponse
        {
            ChunkHeader = chunkConfig.ChunkHeader,
            MaxTokensPerChunk = chunkConfig.MaxTokensPerChunk,
            Overlap = chunkConfig.Overlap,
            Items = chunks.OrderBy(x => x.Order).ToArray()
        };
    }
}
