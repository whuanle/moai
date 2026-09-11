using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands.Responses;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Commands;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="PreUploadTeamOpenApiFileCommand"/> 预上传团队 OpenAPI 文件（json/yaml）.
/// </summary>
public class PreUploadTeamOpenApiFileCommandHandler : IRequestHandler<PreUploadTeamOpenApiFileCommand, PreUploadOpenApiFilePluginCommandResponse>
{
    private static readonly string[] OpenApiFormats = { ".JSON", ".YAML", ".YML" };

    private readonly IStorageService _storageService;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadTeamOpenApiFileCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">文件存储领域服务.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表.</param>
    /// <param name="teamService">团队领域服务.</param>
    public PreUploadTeamOpenApiFileCommandHandler(IStorageService storageService, DatabaseContext databaseContext, IPluginRegistry pluginRegistry, ITeamService teamService)
    {
        _storageService = storageService;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<PreUploadOpenApiFilePluginCommandResponse> Handle(PreUploadTeamOpenApiFileCommand request, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(request.TeamId, request.ContextUserId, cancellationToken);

        await EnsureTeamPluginNameUniquenessAsync(request.TeamId, request.PluginName, cancellationToken);

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

    private async Task EnsureManagerAsync(long teamId, long userId, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理插件.") { StatusCode = 403 };
        }
    }

    private async Task EnsureTeamPluginNameUniquenessAsync(long teamId, string name, CancellationToken cancellationToken)
    {
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.TeamId == teamId && x.PluginName == name && x.IsDeleted == 0, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        if (_pluginRegistry.Get(name) != null)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }
    }
}
