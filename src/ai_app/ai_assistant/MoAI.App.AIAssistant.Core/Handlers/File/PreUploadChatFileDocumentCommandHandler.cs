using MediatR;
using Microsoft.KernelMemory.Pipeline;
using MoAI.App.AIAssistant.Commands.File;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;

namespace MoAI.App.AIAssistant.Handlers.File;

/// <summary>
/// 预上传知识库文件.
/// </summary>
public class PreUploadChatFileDocumentCommandHandler : IRequestHandler<PreUploadChatFileDocumentCommand, PreUploadChatFileDocumentCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadChatFileDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public PreUploadChatFileDocumentCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<PreUploadChatFileDocumentCommandResponse> Handle(PreUploadChatFileDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!new MimeTypesDetection().TryGetFileType(request.FileName, out var mimeType))
        {
            throw new BusinessException("不支持该文件格式") { StatusCode = 400 };
        }

        var objectKey = FileStoreHelper.GetObjectKey(md5: request.MD5, fileName: request.FileName);
        var ossObjectKey = FileStoreHelper.GetObjectKey(md5: request.MD5, fileName: request.FileName, prefix: $"chat/{request.ChatId}");

        var result = await _mediator.Send(new PreUploadFileCommand
        {
            MD5 = request.MD5,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ObjectKey = ossObjectKey,
            Expiration = TimeSpan.FromMinutes(2)
        });

        return new PreUploadChatFileDocumentCommandResponse
        {
            FileId = result.FileId,
            ObjectKey = objectKey,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
        };
    }
}
