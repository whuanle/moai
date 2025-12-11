using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.Chunkers;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Embeddings.Models;
using MoAI.Wiki.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询知识库文档文件.
/// </summary>
public class QueryWikiDocumentInfoCommandHandler : IRequestHandler<QueryWikiDocumentInfoCommand, QueryWikiDocumentInfoCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiDocumentInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentInfoCommandResponse> Handle(QueryWikiDocumentInfoCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.WikiDocuments.Where(x => x.Id == request.DocumentId);

        var result = await query.Join(_databaseContext.Files, a => a.FileId, b => b.Id, (a, b) => new
        {
            DocumentId = a.Id,
            FileName = a.FileName,
            FileSize = b.FileSize,
            ContentType = b.ContentType,
            CreateTime = a.CreateTime,
            CreateUserId = a.CreateUserId,
            UpdateTime = a.UpdateTime,
            UpdateUserId = a.UpdateUserId,
            Embedding = a.IsEmbedding,
            SliceConfig = a.SliceConfig
        }).FirstOrDefaultAsync();

        if (result == null)
        {
            throw new BusinessException("文档不存在") { StatusCode = 404 };
        }

        var chunkConfig = result.SliceConfig.JsonToObject<PlainTextChunkerOptions>()!;

        var response = new QueryWikiDocumentInfoCommandResponse
        {
            DocumentId = request.DocumentId,
            FileName = result.FileName,
            FileSize = result.FileSize,
            ContentType = result.ContentType,
            CreateTime = result.CreateTime,
            CreateUserId = result.CreateUserId,
            UpdateTime = result.UpdateTime,
            UpdateUserId = result.UpdateUserId,
            PartionConfig = new DocumentTextPartionConfig
            {
                ChunkHeader = chunkConfig.ChunkHeader,
                Overlap = chunkConfig.Overlap,
                MaxTokensPerChunk = chunkConfig.MaxTokensPerChunk
            }
        };

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = new List<QueryWikiDocumentInfoCommandResponse> { response }
        });

        return response;
    }
}
