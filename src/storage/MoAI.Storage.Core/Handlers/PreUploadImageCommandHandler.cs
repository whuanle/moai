using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;
using MoAI.Storage.Helpers;

namespace MoAI.Storage.Handlers;

/// <summary>
/// <inheritdoc cref="PreUploadImageCommand"/>
/// </summary>
public class PreUploadImageCommandHandler : IRequestHandler<PreUploadImageCommand, PreUploadImageCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadImageCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator">MediatR.</param>
    public PreUploadImageCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<PreUploadImageCommandResponse> Handle(PreUploadImageCommand request, CancellationToken cancellationToken)
    {
        // 验证是否为图片格式
        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrEmpty(extension) || !FileStoreHelper.ImageExtensions.Contains(extension))
        {
            throw new BusinessException("只支持上传图片格式: " + string.Join(", ", FileStoreHelper.ImageExtensions)) { StatusCode = 400 };
        }

        // 生成 ObjectKey，使用 images/ 前缀
        var objectKey = FileStoreHelper.GetObjectKey(md5: request.MD5, fileName: request.FileName, prefix: "images");

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

        return new PreUploadImageCommandResponse
        {
            FileId = result.FileId,
            ObjectKey = objectKey,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
        };
    }
}
