using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteExternalWikiDocumentsCommand"/>
/// </summary>
public class DeleteExternalWikiDocumentsCommandHandler : IRequestHandler<DeleteExternalWikiDocumentsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteExternalWikiDocumentsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    public DeleteExternalWikiDocumentsCommandHandler(DatabaseContext databaseContext, IStorageService storageService, IExternalWikiAuthorizer externalWikiAuthorizer)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _externalWikiAuthorizer = externalWikiAuthorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteExternalWikiDocumentsCommand request, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var documentIds = request.DocumentIds.ToHashSet();
        var documents = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && documentIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        if (documents.Length == 0)
        {
            throw new BusinessException("未指定要删除的文档.") { StatusCode = 404 };
        }

        _databaseContext.WikiDocuments.RemoveRange(documents);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 删除关联切片/元数据
        var deletedDocumentIds = documents.Select(x => x.Id).ToArray();
        var chunks = await _databaseContext.WikiDocumentChunkContents
            .Where(x => deletedDocumentIds.Contains(x.DocumentId))
            .ToArrayAsync(cancellationToken);
        if (chunks.Length > 0)
        {
            _databaseContext.WikiDocumentChunkContents.RemoveRange(chunks);
        }

        var metadata = await _databaseContext.WikiDocumentChunkMetadata
            .Where(x => deletedDocumentIds.Contains(x.DocumentId))
            .ToArrayAsync(cancellationToken);
        if (metadata.Length > 0)
        {
            _databaseContext.WikiDocumentChunkMetadata.RemoveRange(metadata);
        }

        // 删除 oss 文件
        var fileIds = documents.Select(x => (long)x.FileId).ToArray();
        await _storageService.DeleteFilesAsync(fileIds, cancellationToken);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
