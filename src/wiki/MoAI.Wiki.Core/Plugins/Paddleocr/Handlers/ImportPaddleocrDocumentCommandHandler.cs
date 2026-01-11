using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Team.Models;
using MoAI.Wiki.Plugins.Paddleocr.Commands;
using MoAI.Wiki.Wikis.Queries;
using System.Text;

namespace MoAI.Wiki.Plugins.Paddleocr.Handlers;

/// <summary>
/// <inheritdoc cref="ImportPaddleocrDocumentCommand"/>
/// </summary>
public class ImportPaddleocrDocumentCommandHandler : IRequestHandler<ImportPaddleocrDocumentCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportPaddleocrDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public ImportPaddleocrDocumentCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(ImportPaddleocrDocumentCommand request, CancellationToken cancellationToken)
    {
        var fileName = $"{request.Title}.md";
        var contentBytes = Encoding.UTF8.GetBytes(request.MarkdownContent);
        using var memoryStream = new MemoryStream(contentBytes);

        memoryStream.Position = 0;
        var md5Hash = FileStoreHelper.CalculateFileMd5(memoryStream);
        memoryStream.Position = 0;

        // 生成 ObjectKey
        var objectKey = FileStoreHelper.GetObjectKey(md5: md5Hash, fileName: fileName, prefix: $"wiki/{request.WikiId}");

        // 检查是否已存在相同文件
        var existDocument = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && x.ObjectKey == objectKey)
            .Select(x => x.FileName)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(existDocument))
        {
            throw new BusinessException("已存在内容一致的文档 {0}", existDocument) { StatusCode = 409 };
        }

        // 上传文件
        memoryStream.Position = 0;
        var uploadResult = await _mediator.Send(
            new UploadFileStreamCommand
            {
                ObjectKey = objectKey,
                MD5 = md5Hash,
                ContentType = FileStoreHelper.GetMimeType(fileName),
                FileStream = memoryStream,
                FileSize = contentBytes.Length
            },
            cancellationToken);

        // 创建文档记录
        var documentEntity = new WikiDocumentEntity
        {
            FileId = uploadResult.FileId,
            WikiId = request.WikiId,
            FileName = fileName,
            ObjectKey = objectKey,
            FileType = ".md"
        };

        await _databaseContext.WikiDocuments.AddAsync(documentEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt { Value = documentEntity.Id };
    }
}
