using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;
using System.Transactions;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// 完成 openapi 文件上传，并拆解生成到数据库.
/// </summary>
public class ImportOpenApiPluginCommandHandler : IRequestHandler<ImportOpenApiPluginCommand, SimpleGuid>
{
    private readonly IStorageService _storageService;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly ILogger<ImportOpenApiPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOpenApiPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">文件存储领域服务.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表，用于校验系统插件 key 不重复.</param>
    /// <param name="logger">日志.</param>
    public ImportOpenApiPluginCommandHandler(IStorageService storageService, DatabaseContext databaseContext, IPluginRegistry pluginRegistry, ILogger<ImportOpenApiPluginCommandHandler> logger)
    {
        _storageService = storageService;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(ImportOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        var fileEntity = await _databaseContext.Files
            .FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);

        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        await EnsurePluginNameUniquenessAsync(request.Name, cancellationToken);

        await _storageService.CompleteAsync(request.FileId, true, cancellationToken);

        // 拉取完整的 openapi 文件
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
            PluginName = request.Name,
            Title = request.Title,
            Type = (int)PluginType.OpenApi,
            IsPublic = request.IsPublic,
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
