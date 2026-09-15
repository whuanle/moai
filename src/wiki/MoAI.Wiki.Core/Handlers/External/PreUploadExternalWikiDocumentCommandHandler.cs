using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="PreUploadExternalWikiDocumentCommand"/>
/// </summary>
public class PreUploadExternalWikiDocumentCommandHandler : IRequestHandler<PreUploadExternalWikiDocumentCommand, PreUploadWikiDocumentCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadExternalWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    public PreUploadExternalWikiDocumentCommandHandler(DatabaseContext databaseContext, IStorageService storageService, IExternalWikiAuthorizer externalWikiAuthorizer)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _externalWikiAuthorizer = externalWikiAuthorizer;
    }

    /// <inheritdoc/>
    public async Task<PreUploadWikiDocumentCommandResponse> Handle(PreUploadExternalWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrEmpty(extension) || !FileStoreHelper.DocumentFormats.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("不支持该文件格式.") { StatusCode = 400 };
        }

        var objectKey = FileStoreHelper.GetObjectKey(sha256: request.SHA256, fileName: request.FileName, prefix: $"wiki/{request.WikiId}");

        // 同一个知识库下不能有同 key 文件
        var existDocument = await _databaseContext.WikiDocuments
            .AnyAsync(x => x.WikiId == request.WikiId && x.ObjectKey == objectKey && x.IsDeleted == 0, cancellationToken);
        if (existDocument)
        {
            throw new BusinessException("知识库已上传过该文件.") { StatusCode = 409 };
        }

        var result = await _storageService.PreUploadAsync(new PreUploadFileCommand
        {
            SHA256 = request.SHA256,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(2)
        }, cancellationToken);

        if (result.IsExist)
        {
            var existWikiDocument = await _databaseContext.WikiDocuments
                .FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.FileId == result.FileId && x.IsDeleted == 0, cancellationToken);
            if (existWikiDocument != null)
            {
                throw new BusinessException("同一个知识库下不能有相同文件.") { StatusCode = 409 };
            }
        }

        return new PreUploadWikiDocumentCommandResponse
        {
            FileId = result.FileId,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
        };
    }
}
