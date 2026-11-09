using MediatR;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Models;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// 知识图谱文件导入预上传命令处理器（公开 chat 目录，与对话附件同目录但白名单独立为提取能力全集）.
/// </summary>
public class PreUploadGraphFileCommandHandler : IRequestHandler<PreUploadGraphFileCommand, PreUploadFileCommandResponse>
{
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadGraphFileCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    public PreUploadGraphFileCommandHandler(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<PreUploadFileCommandResponse> Handle(PreUploadGraphFileCommand request, CancellationToken cancellationToken)
    {
        var objectKey = FileStoreHelper.ToPublicObjectKey(FileStoreHelper.GetObjectKey(request.SHA256, request.FileName, "chat"));

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
