using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Commands.Responses;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// 预上传 openapi 文件，支持 json、yaml.
/// </summary>
public class PreUploadOpenApiFilePluginCommandHandler : IRequestHandler<PreUploadOpenApiFilePluginCommand, PreUploadOpenApiFilePluginCommandResponse>
{
    private static readonly string[] OpenApiFormats = { ".JSON", ".YAML", ".YML" };

    private readonly IStorageService _storageService;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadOpenApiFilePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">文件存储领域服务.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表，用于校验系统插件 key 不重复.</param>
    public PreUploadOpenApiFilePluginCommandHandler(IStorageService storageService, DatabaseContext databaseContext, IPluginRegistry pluginRegistry)
    {
        _storageService = storageService;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
    }

    /// <inheritdoc/>
    public async Task<PreUploadOpenApiFilePluginCommandResponse> Handle(PreUploadOpenApiFilePluginCommand request, CancellationToken cancellationToken)
    {
        await EnsurePluginNameUniquenessAsync(request.PluginName, cancellationToken);

        var extension = Path.GetExtension(request.FileName).ToUpperInvariant();
        if (!OpenApiFormats.Contains(extension))
        {
            throw new BusinessException("不支持的文件格式，请导入 .json/.yaml/.yml 文件") { StatusCode = 400 };
        }

        var objectKey = FileStoreHelper.GetObjectKey(request.SHA256, request.FileName, "plugin");

        var result = await _storageService.PreUploadAsync(new PreUploadFileCommand
        {
            SHA256 = request.SHA256,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(2),
        }, cancellationToken);

        return new PreUploadOpenApiFilePluginCommandResponse
        {
            FileId = result.FileId,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration,
        };
    }

    private async Task EnsurePluginNameUniquenessAsync(string name, CancellationToken cancellationToken)
    {
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == name, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        if (_pluginRegistry.Get(name) != null)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }
    }
}
