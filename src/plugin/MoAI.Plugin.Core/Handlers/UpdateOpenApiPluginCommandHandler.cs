// <copyright file="ImportOpenApiPluginCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.Plugin.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Queries;
using MoAI.Store.Enums;
using System.Transactions;

namespace MaomiAI.Plugin.Core.Commands;

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
        var pluginEntity = await _databaseContext.Plugins.Where(x => x.Id == request.PluginId).FirstOrDefaultAsync(cancellationToken);

        if (pluginEntity == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        if (request.FileId == request.FileId)
        {
            pluginEntity.PluginName = request.Name;
            pluginEntity.Description = request.Description;

            _databaseContext.Update(pluginEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
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
            Visibility = FileVisibility.Private,
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

        pluginEntity.Title = TruncateString(apiReaderResult.OpenApiDocument.Info.Title, 255);

        _databaseContext.Plugins.Update(pluginEntity);
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

        await _databaseContext.SoftDeleteAsync(_databaseContext.PluginFunctions.Where(x => x.PluginId == pluginEntity.Id));

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

    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}