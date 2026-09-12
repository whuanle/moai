using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Skill.Commands;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="PreUploadSkillFileCommand"/>
/// </summary>
public class PreUploadSkillFileCommandHandler : IRequestHandler<PreUploadSkillFileCommand, PreUploadSkillFileCommandResponse>
{
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadSkillFileCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    public PreUploadSkillFileCommandHandler(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<PreUploadSkillFileCommandResponse> Handle(PreUploadSkillFileCommand request, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrEmpty(extension) || !FileStoreHelper.SkillPackageFormats.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("不支持该技能包文件格式.") { StatusCode = 400 };
        }

        var objectKey = FileStoreHelper.GetObjectKey(sha256: request.SHA256, fileName: request.FileName, prefix: "skill");

        var result = await _storageService.PreUploadAsync(new PreUploadFileCommand
        {
            SHA256 = request.SHA256,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(2)
        }, cancellationToken);

        return new PreUploadSkillFileCommandResponse
        {
            IsExist = result.IsExist,
            FileId = result.FileId,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration,
        };
    }
}
