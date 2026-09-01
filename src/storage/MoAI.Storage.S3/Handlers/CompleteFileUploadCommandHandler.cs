using MediatR;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Models;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// 完成文件上传命令处理器.
/// </summary>
public class CompleteFileUploadCommandHandler : IRequestHandler<CompleteFileUploadCommand, CompleteFileUploadCommandResponse>
{
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteFileUploadCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    public CompleteFileUploadCommandHandler(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<CompleteFileUploadCommandResponse> Handle(CompleteFileUploadCommand request, CancellationToken cancellationToken)
    {
        var objectKey = await _storageService.CompleteAsync(request.FileId, request.IsSuccess, cancellationToken);

        // 公开文件返回 /static 免登录访问地址，私有文件不返回静态地址
        var accessUrl = FileStoreHelper.IsPublicObjectKey(objectKey)
            ? _storageService.GetPublicFileUrl(objectKey).ToString()
            : null;

        return new CompleteFileUploadCommandResponse
        {
            FileId = request.FileId,
            ObjectKey = objectKey,
            AccessUrl = accessUrl
        };
    }
}
