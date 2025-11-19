using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.Models;
using MoAI.Storage.Commands;
using MoAI.Storage.Queries;
using MoAI.Storage.Queries.Response;
using System.Transactions;

namespace MoAIPlugin.Core.Commands;

/// <summary>
/// 完成 openapi 文件上传，并拆解生成到数据库.
/// </summary>
public class ImportOpenApiPluginCommandHandler : IRequestHandler<ImportOpenApiPluginCommand, SimpleInt>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<ImportOpenApiPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOpenApiPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    /// <param name="logger"></param>
    public ImportOpenApiPluginCommandHandler(IMediator mediator, DatabaseContext databaseContext, ILogger<ImportOpenApiPluginCommandHandler> logger)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(ImportOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        var fileEntity = await _databaseContext.Files.Where(x => x.Id == request.FileId).FirstOrDefaultAsync();
        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        // 检查插件是否同名
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == request.Name, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        exists = await _databaseContext.PluginInternals
            .AnyAsync(x => x.PluginName == request.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessException("插件名称已存在") { StatusCode = 409 };
        }

        await _mediator.Send(new ComplateFileUploadCommand { FileId = request.FileId, IsSuccess = true });

        // 拉取完整的 openapi 文件
        var openFilePath = await _mediator.Send(new QueryFileLocalPathCommand
        {
            ObjectKey = fileEntity.ObjectKey
        });

        ReadResult apiReaderResult;

        try
        {
            apiReaderResult = await ReadOpemApiFileAsync(openFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import file.");
            throw new BusinessException("导入文件失败.");
        }

        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        var pluginEntity = new PluginEntity
        {
            OpenapiFileName = request.FileName,
            PluginName = request.Name,
            Description = request.Description,
            Headers = TextToJsonExtensions.ToJsonString(Array.Empty<KeyValueString>()),
            Queries = TextToJsonExtensions.ToJsonString(Array.Empty<KeyValueString>()),
            OpenapiFileId = fileEntity.Id,
            Server = apiReaderResult.OpenApiDocument.Servers.FirstOrDefault()?.Url ?? string.Empty,
            Type = (int)PluginType.OpenApi,
            Title = request.Title,
            IsPublic = request.IsPublic,
            ClassifyId = request.ClassifyId
        };

        await _databaseContext.Plugins.AddAsync(pluginEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        List<PluginFunctionEntity> pluginFunctionEntities = new();

        foreach (var pathEntry in apiReaderResult.OpenApiDocument.Paths)
        {
            // 接口名称
            var operationId = pathEntry.Value.Operations.First().Value.OperationId;
            var summary = pathEntry.Value.Operations.First().Value.Summary;

            pluginFunctionEntities.Add(new PluginFunctionEntity
            {
                Name = operationId,
                Summary = summary,
                Path = pathEntry.Key,
                PluginId = pluginEntity.Id,
            });
        }

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return pluginEntity.Id;
    }

    private static async Task<ReadResult> ReadOpemApiFileAsync(QueryFileLocalPathCommandResponse openFilePath)
    {
        // 解析 openapi 文件，读取每个接口
        using var fileStream = new FileStream(openFilePath.FilePath, FileMode.Open);
        var reader = new OpenApiStreamReader();
        var apiReaderResult = await reader.ReadAsync(fileStream);
        return apiReaderResult;
    }
}
