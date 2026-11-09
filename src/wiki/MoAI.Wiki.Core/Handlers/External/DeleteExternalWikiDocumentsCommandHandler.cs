using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteExternalWikiDocumentsCommand"/>
/// </summary>
public class DeleteExternalWikiDocumentsCommandHandler : IRequestHandler<DeleteExternalWikiDocumentsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly WikiDocumentRemovalService _removalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteExternalWikiDocumentsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="removalService">文档删除领域服务（向量/切片/元数据/正文/映射/OSS/图片全链路清理）.</param>
    public DeleteExternalWikiDocumentsCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer, WikiDocumentRemovalService removalService)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _removalService = removalService;
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

        await _removalService.RemoveDocumentsAsync((int)request.WikiId, documents, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
