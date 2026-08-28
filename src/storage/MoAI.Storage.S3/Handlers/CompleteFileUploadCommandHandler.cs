using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Services;

namespace MoAI.Storage.Handlers;

/// <summary>
/// 完成文件上传命令处理器.
/// </summary>
public class CompleteFileUploadCommandHandler : IRequestHandler<CompleteFileUploadCommand, EmptyCommandResponse>
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
    public async Task<EmptyCommandResponse> Handle(CompleteFileUploadCommand request, CancellationToken cancellationToken)
    {
        await _storageService.CompleteAsync(request.FileId, request.IsSuccess, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
