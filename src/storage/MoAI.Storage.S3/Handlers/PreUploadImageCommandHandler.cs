using MediatR;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Models;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// 图片预上传命令处理器.
/// </summary>
public class PreUploadImageCommandHandler : IRequestHandler<PreUploadImageCommand, PreUploadFileCommandResponse>
{
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadImageCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    public PreUploadImageCommandHandler(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<PreUploadFileCommandResponse> Handle(PreUploadImageCommand request, CancellationToken cancellationToken)
    {
        var objectKey = FileStoreHelper.ToPublicObjectKey(FileStoreHelper.GetObjectKey(request.SHA256, request.FileName, "images"));

        var result = await _storageService.PreUploadAsync(new PreUploadFileCommand
        {
            SHA256 = request.SHA256,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(5)
        }, cancellationToken);

        return new PreUploadFileCommandResponse
        {
            IsExist = result.IsExist,
            FileId = result.FileId,
            ObjectKey = objectKey,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
        };
    }
}
