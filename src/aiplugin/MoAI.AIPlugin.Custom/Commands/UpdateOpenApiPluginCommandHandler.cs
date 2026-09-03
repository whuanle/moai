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
public class UpdateOpenApiPluginCommandHandler : IRequestHandler<UpdateOpenApiPluginCommand, EmptyCommandResponse>
{
    private readonly IStorageService _storageService;
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly ILogger<UpdateOpenApiPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOpenApiPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">文件存储领域服务.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="pluginRegistry">插件注册表，用于校验系统插件 key 不重复.</param>
    /// <param name="logger">日志.</param>
    public UpdateOpenApiPluginCommandHandler(IStorageService storageService, DatabaseContext databaseContext, IPluginRegistry pluginRegistry, ILogger<UpdateOpenApiPluginCommandHandler> logger)
    {
        _storageService = storageService;
        _databaseContext = databaseContext;
        _pluginRegistry = pluginRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        var pluginEntity = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 409 };
        }

        var pluginCustomEntity = await _databaseContext.PluginCustoms
            .FirstOrDefaultAsync(x => x.Id == pluginEntity.PluginId && x.Type == (int)PluginType.OpenApi, cancellationToken);

        if (pluginCustomEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        await EnsurePluginNameUniquenessAsync(request.Name, request.PluginId, cancellationToken);

        if (pluginEntity.TeamId == 0)
        {
            pluginEntity.IsPublic = request.IsPublic;
        }

        pluginEntity.Description = request.Description;
        pluginEntity.PluginName = request.Name;
        pluginEntity.ClassifyId = request.ClassifyId;

        pluginCustomEntity.Queries = request.Query.ToJsonString();
        pluginCustomEntity.Headers = request.Header.ToJsonString();
        pluginCustomEntity.Server = request.ServerUrl.ToString();

        // 未覆盖新的 openapi 文件
        if (request.FileId == 0 || request.FileId == pluginCustomEntity.OpenapiFileId)
        {
            _databaseContext.Update(pluginEntity);
            _databaseContext.Update(pluginCustomEntity);

            await _databaseContext.SaveChangesAsync(cancellationToken);
            return EmptyCommandResponse.Default;
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

        return EmptyCommandResponse.Default;
    }

    private async Task EnsurePluginNameUniquenessAsync(string name, Guid pluginId, CancellationToken cancellationToken)
    {
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == name && x.Id != pluginId, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }

        if (_pluginRegistry.Get(name) != null)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }
    }
}
