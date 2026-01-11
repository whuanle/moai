using MediatR;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;
using MoAI.Storage.Helpers;

namespace MoAI.Storage.Handlers;

/// <summary>
/// <inheritdoc cref="PreUploadTempFileCommand"/>
/// </summary>
public class PreUploadTempFileCommandHandler : IRequestHandler<PreUploadTempFileCommand, PreUploadTempFileCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadTempFileCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator">MediatR.</param>
    public PreUploadTempFileCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<PreUploadTempFileCommandResponse> Handle(PreUploadTempFileCommand request, CancellationToken cancellationToken)
    {
        // 生成 ObjectKey，使用 temp/ 前缀
        var objectKey = FileStoreHelper.GetObjectKey(md5: request.MD5, fileName: request.FileName, prefix: "temp");

        // 调用通用预上传命令
        var result = await _mediator.Send(
            new PreUploadFileCommand
            {
                MD5 = request.MD5,
                ContentType = request.ContentType,
                FileSize = request.FileSize,
                ObjectKey = objectKey,
                Expiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken);

        return new PreUploadTempFileCommandResponse
        {
            FileId = result.FileId,
            ObjectKey = objectKey,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
        };
    }
}
