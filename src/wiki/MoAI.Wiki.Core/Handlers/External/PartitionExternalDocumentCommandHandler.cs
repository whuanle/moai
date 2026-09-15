using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="PartitionExternalDocumentCommand"/>
/// </summary>
public class PartitionExternalDocumentCommandHandler : IRequestHandler<PartitionExternalDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly WikiDocumentProcessingService _processingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartitionExternalDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="processingService">文档内容处理服务.</param>
    public PartitionExternalDocumentCommandHandler(
        DatabaseContext databaseContext,
        IExternalWikiAuthorizer externalWikiAuthorizer,
        WikiDocumentProcessingService processingService)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _processingService = processingService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(PartitionExternalDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await AuthorizeAndLoadDocumentAsync(request.WikiId, request.Caller.TeamId, request.DocumentId, cancellationToken);
        await _processingService.PartitionAsync(
            document.WikiId,
            document.Id,
            request.ChunkSize,
            request.ChunkOverlap,
            request.SplitMode,
            request.OverlapUnit,
            request.SizeUnit,
            request.TokenEncodingOrModel,
            cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private async Task<Database.Entities.WikiDocumentEntity> AuthorizeAndLoadDocumentAsync(long wikiId, long teamId, long documentId, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(wikiId, teamId, cancellationToken);

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == documentId && x.WikiId == wikiId && x.IsDeleted == 0, cancellationToken);
        if (document == null)
        {
            throw new BusinessException("知识库文档不存在.") { StatusCode = 404 };
        }

        return document;
    }
}
