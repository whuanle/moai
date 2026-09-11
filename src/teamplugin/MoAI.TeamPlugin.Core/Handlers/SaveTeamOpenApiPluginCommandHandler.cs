using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Commands;
using System.Transactions;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="SaveTeamOpenApiPluginCommand"/> 导入/更新团队 OpenAPI 插件.
/// </summary>
public class SaveTeamOpenApiPluginCommandHandler : IRequestHandler<SaveTeamOpenApiPluginCommand, SimpleGuid>
{
    private readonly IStorageService _storageService;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly ITeamService _teamService;
    private readonly ILogger<SaveTeamOpenApiPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveTeamOpenApiPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">文件存储领域服务.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表，用于校验系统插件 key 不重复.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="logger">日志.</param>
    public SaveTeamOpenApiPluginCommandHandler(IStorageService storageService, DatabaseContext databaseContext, IPluginRegistry pluginRegistry, ITeamService teamService, ILogger<SaveTeamOpenApiPluginCommandHandler> logger)
    {
        _storageService = storageService;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
        _teamService = teamService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(SaveTeamOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (request.PluginId.HasValue && request.PluginId != Guid.Empty)
        {
            return await UpdateAsync(request, cancellationToken);
        }

        return await CreateAsync(request, cancellationToken);
    }

    private async Task<SimpleGuid> CreateAsync(SaveTeamOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        var fileEntity = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);

        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        await EnsureTeamPluginNameUniquenessAsync(request.TeamId, request.Name, null, cancellationToken);

        await _storageService.CompleteAsync(request.FileId, true, cancellationToken);

        var fileReadResult = await _storageService.ReadAsync(fileEntity.ObjectKey, cancellationToken);
        OpenApiParseResult parseResult;
        try
        {
            parseResult = await OpenApiDocumentParser.ParseAsync(fileReadResult.FileStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import file.");
            throw new BusinessException("导入文件失败.");
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var pluginCustomEntity = new PluginCustomEntity
        {
            OpenapiFileName = request.FileName,
            Headers = Array.Empty<KeyValueString>().ToJsonString(),
            Queries = Array.Empty<KeyValueString>().ToJsonString(),
            OpenapiFileId = fileEntity.Id,
            Server = parseResult.Server,
            Type = (int)PluginType.OpenApi,
        };

        await _databaseContext.PluginCustoms.AddAsync(pluginCustomEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var pluginEntity = new PluginEntity
        {
            IsSystem = false,
            TeamId = (int)request.TeamId,
            PluginName = request.Name,
            Title = request.Title,
            Type = (int)PluginType.OpenApi,
            IsPublic = true,
            ClassifyId = request.ClassifyId,
            PluginId = pluginCustomEntity.Id,
            Description = request.Description,
        };

        await _databaseContext.Plugins.AddAsync(pluginEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var pluginFunctionEntities = parseResult.Functions.Select(x => new PluginFunctionEntity
        {
            Name = x.Name,
            Summary = x.Summary ?? string.Empty,
            Path = x.Path,
            PluginCustomId = pluginCustomEntity.Id,
        }).ToList();

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return (SimpleGuid)pluginEntity.Id;
    }

    private async Task<SimpleGuid> UpdateAsync(SaveTeamOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId && x.TeamId == request.TeamId && x.IsDeleted == 0, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("团队插件不存在") { StatusCode = 404 };
        }

        var pluginCustomEntity = await _databaseContext.PluginCustoms
            .FirstOrDefaultAsync(x => x.Id == pluginEntity.PluginId && x.Type == (int)PluginType.OpenApi, cancellationToken);

        if (pluginCustomEntity == null)
        {
            throw new BusinessException("团队插件不存在") { StatusCode = 404 };
        }

        await EnsureTeamPluginNameUniquenessAsync(request.TeamId, request.Name, pluginEntity.Id, cancellationToken);

        pluginEntity.Title = request.Title;
        pluginEntity.Description = request.Description;
        pluginEntity.PluginName = request.Name;
        pluginEntity.ClassifyId = request.ClassifyId;

        // 未覆盖新的 openapi 文件
        if (request.FileId == 0 || request.FileId == pluginCustomEntity.OpenapiFileId)
        {
            _databaseContext.Update(pluginEntity);
            _databaseContext.Update(pluginCustomEntity);

            await _databaseContext.SaveChangesAsync(cancellationToken);
            return (SimpleGuid)pluginEntity.Id;
        }

        var fileEntity = await _databaseContext.Files.FirstOrDefaultAsync(x => x.Id == request.FileId && x.IsUploaded, cancellationToken);
        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        await _storageService.CompleteAsync(request.FileId, true, cancellationToken);

        var fileReadResult = await _storageService.ReadAsync(fileEntity.ObjectKey, cancellationToken);
        OpenApiParseResult parseResult;
        try
        {
            parseResult = await OpenApiDocumentParser.ParseAsync(fileReadResult.FileStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import file.");
            throw new BusinessException("导入文件失败.");
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        pluginCustomEntity.OpenapiFileId = fileEntity.Id;
        pluginCustomEntity.OpenapiFileName = request.FileName;

        _databaseContext.PluginCustoms.Update(pluginCustomEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var pluginFunctionEntities = parseResult.Functions.Select(x => new PluginFunctionEntity
        {
            Name = x.Name,
            Summary = x.Summary ?? string.Empty,
            Path = x.Path,
            PluginCustomId = pluginCustomEntity.Id,
        }).ToList();

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return (SimpleGuid)pluginEntity.Id;
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

    private async Task EnsureTeamPluginNameUniquenessAsync(long teamId, string name, Guid? excludePluginId, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Plugins.Where(x => x.TeamId == teamId && x.PluginName == name && x.IsDeleted == 0);
        if (excludePluginId.HasValue)
        {
            query = query.Where(x => x.Id != excludePluginId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        if (_pluginRegistry.Get(name) != null)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }
    }
}
