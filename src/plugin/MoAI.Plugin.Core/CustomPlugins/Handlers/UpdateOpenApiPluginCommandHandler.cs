using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Storage.Commands;
using MoAI.Storage.Queries;
using MoAI.Storage.Queries.Response;
using System.Transactions;

namespace MoAIPlugin.Core.Commands;

/// <summary>
/// 完成 openapi 文件上传，并拆解生成到数据库.
/// </summary>
public class UpdateOpenApiPluginCommandHandler : IRequestHandler<UpdateOpenApiPluginCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<UpdateOpenApiPluginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOpenApiPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    /// <param name="logger"></param>
    public UpdateOpenApiPluginCommandHandler(IMediator mediator, DatabaseContext databaseContext, ILogger<UpdateOpenApiPluginCommandHandler> logger)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        // 检查插件有同名插件
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

        // 检查插件是否同名
        var exists = await _databaseContext.Plugins
            .AnyAsync(x => x.PluginName == request.Name && x.Id != request.PluginId, cancellationToken);

        if (exists)
        {
            throw new BusinessException("插件名称已被使用") { StatusCode = 409 };
        }

        pluginEntity.IsPublic = request.IsPublic;
        pluginEntity.Description = request.Description;
        pluginEntity.Title = request.Name;
        pluginEntity.PluginName = request.Name;
        pluginEntity.ClassifyId = request.ClassifyId;

        pluginCustomEntity.Queries = request.Query.ToJsonString();
        pluginCustomEntity.Headers = request.Header.ToJsonString();
        pluginCustomEntity.Server = request.ServerUrl.ToString();

        // 没有覆盖 openapi 文件
        if (request.FileId == request.FileId || request.FileId == 0 || request.FileId == pluginCustomEntity.OpenapiFileId)
        {
            _databaseContext.Update(pluginEntity);
            _databaseContext.Update(pluginCustomEntity);

            await _databaseContext.SaveChangesAsync(cancellationToken);
            return EmptyCommandResponse.Default;
        }

        var fileEntity = await _databaseContext.Files.Where(x => x.Id == request.FileId).FirstOrDefaultAsync();
        if (fileEntity == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
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

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        pluginCustomEntity.OpenapiFileId = fileEntity.Id;
        pluginCustomEntity.OpenapiFileName = request.FileName;

        _databaseContext.PluginCustoms.Update(pluginCustomEntity);
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
                PluginCustomId = pluginCustomEntity.Id,
            });
        }

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomEntity.Id));

        await _databaseContext.PluginFunctions.AddRangeAsync(pluginFunctionEntities, cancellationToken);
        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
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