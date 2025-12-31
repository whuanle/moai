using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.Pipeline;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Wiki.Documents.Models;
using MoAI.Wiki.Helpers;

namespace MoAI.Wiki.Documents.Handlers;

/// <summary>
/// 预上传知识库文件.
/// </summary>
public class PreUploadWikiDocumentCommandHandler : IRequestHandler<PreUploadWikiDocumentCommand, PreUploadWikiDocumentCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public PreUploadWikiDocumentCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<PreUploadWikiDocumentCommandResponse> Handle(PreUploadWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!DocumentTypeHelper.IsSupportedDocumentType(request.FileName, out var mimeType))
        {
            throw new BusinessException("不支持该文件格式") { StatusCode = 400 };
        }

        var objectKey = FileStoreHelper.GetObjectKey(md5: request.MD5, fileName: request.FileName, prefix: $"wiki/{request.WikiId}");

        // 同一个知识库下不能有同key文件.
        var existDocument = await _databaseContext.WikiDocuments.Where(x => x.ObjectKey == objectKey).AnyAsync();

        if (existDocument)
        {
            throw new BusinessException("知识库已上传过该文件") { StatusCode = 409 };
        }

        var result = await _mediator.Send(new PreUploadFileCommand
        {
            MD5 = request.MD5,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(2)
        });

        if (result.IsExist)
        {
            // 但是知识库文档没有
            var exisWikitDocument = await _databaseContext.WikiDocuments.FirstOrDefaultAsync(x => x.WikiId == request.WikiId && x.FileId == result.FileId);

            if (exisWikitDocument != null)
            {
                throw new BusinessException("同一个知识库下不能有相同文件 {0}", exisWikitDocument.FileName) { StatusCode = 409 };
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
