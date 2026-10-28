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
/// <inheritdoc cref="DownloadExternalWikiDocumentCommand"/>
/// </summary>
public class DownloadExternalWikiDocumentCommandHandler : IRequestHandler<DownloadExternalWikiDocumentCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadExternalWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    public DownloadExternalWikiDocumentCommandHandler(DatabaseContext databaseContext, IStorageService storageService, IExternalWikiAuthorizer externalWikiAuthorizer)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _externalWikiAuthorizer = externalWikiAuthorizer;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(DownloadExternalWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId && x.WikiId == request.WikiId, cancellationToken);
        if (document == null)
        {
            throw new BusinessException("文档不存在.") { StatusCode = 404 };
        }

        var documentFile = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.Id == document.FileId, cancellationToken);
        if (documentFile == null)
        {
            throw new BusinessException("未找到文档文件.") { StatusCode = 404 };
        }

        var url = await _storageService.GetDownloadUrlAsync(documentFile.ObjectKey, document.FileName, TimeSpan.FromMinutes(2), cancellationToken);

        return new SimpleString
        {
            Value = url.ToString()
        };
    }
}
