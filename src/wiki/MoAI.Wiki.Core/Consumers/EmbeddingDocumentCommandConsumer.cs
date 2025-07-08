// <copyright file="EmbeddingDocumentCommandConsumer.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi.MQ;
using MaomiAI.Document.Core.Consumers.Events;
using MaomiAI.Document.Core.Services;
using MaomiAI.Document.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Storage.Commands;
using MoAI.Store.Enums;
using MoAI.Wiki.Models;

namespace MaomiAI.Document.Core.Consumers;

/// <summary>
/// 文档向量化.
/// </summary>
[Consumer("embedding_document", Qos = 1)]
public class EmbeddingDocumentCommandConsumer : IConsumer<EmbeddingDocumentEvent>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly CustomKernelMemoryBuilder _customKernelMemoryBuilder;
    private readonly IMediator _mediator;
    private readonly ILogger<EmbeddingDocumentCommandConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentCommandConsumer"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="logger"></param>
    public EmbeddingDocumentCommandConsumer(IServiceProvider serviceProvider, ILogger<EmbeddingDocumentCommandConsumer> logger)
    {
        _databaseContext = serviceProvider.GetRequiredService<DatabaseContext>();
        _systemOptions = serviceProvider.GetRequiredService<SystemOptions>();
        _customKernelMemoryBuilder = serviceProvider.GetRequiredService<CustomKernelMemoryBuilder>();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, EmbeddingDocumentEvent message)
    {
        var documentTask = await _databaseContext.WikiDocumentTasks
             .FirstOrDefaultAsync(x => x.DocumentId == message.DocumentId && x.Id == message.TaskId);

        // 不需要处理
        if (documentTask == null || documentTask.State > (int)FileEmbeddingState.Processing)
        {
            return;
        }

        documentTask.State = (int)FileEmbeddingState.Processing;
        documentTask.Message = "任务开始处理";
        _databaseContext.WikiDocumentTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync();

        var documentFile = await _databaseContext.WikiDocuments
            .Where(x => x.Id == message.DocumentId)
            .Join(_databaseContext.Files, a => a.FileId, b => b.Id, (a, b) => new
            {
                a.FileId,
                b.FileName,
                FilePath = b.ObjectKey,
            }).FirstOrDefaultAsync();

        if (documentFile == null)
        {
            documentTask.State = (int)FileEmbeddingState.Failed;
            documentTask.Message = "文件不存在";
            await _databaseContext.SaveChangesAsync();
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), DateTimeOffset.Now.Ticks.ToString());
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        // 下载文件
        var filePath = Path.Combine(tempDir, documentFile.FileName);

        await _mediator.Send(new DownloadFileCommand
        {
            Visibility = FileVisibility.Private,
            ObjectKey = documentFile.FilePath,
            StoreFilePath = filePath
        });

        var teamWikiConfig = await _databaseContext.Wikis
        .Where(x => x.Id == documentTask.WikiId)
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
        {
            WikiConfig = new WikiConfig
            {
                EmbeddingDimensions = a.EmbeddingDimensions,
                EmbeddingBatchSize = a.EmbeddingBatchSize,
                MaxRetries = a.MaxRetries,
                EmbeddingModelTokenizer = a.EmbeddingModelTokenizer,
                EmbeddingModelId = a.EmbeddingModelId,
            },
            AiEndpoint = new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = Enum.Parse<AiModelType>(x.AiModelType, true),
                Provider = Enum.Parse<AiProvider>(x.AiProvider, true),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput,
                Key = x.Key,
            }
        }).FirstOrDefaultAsync();

        if (teamWikiConfig == null)
        {
            documentTask.State = (int)FileEmbeddingState.Failed;
            documentTask.Message = "知识库未配置向量化模型";
            await _databaseContext.SaveChangesAsync();
            return;
        }

        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder().WithSimpleFileStorage(Path.GetTempPath());

        _customKernelMemoryBuilder.ConfigEmbeddingModel(memoryBuilder, teamWikiConfig.AiEndpoint, teamWikiConfig.WikiConfig);

        var memoryClient = memoryBuilder.WithoutTextGenerator()

            .WithPostgresMemoryDb(new PostgresConfig
            {
                ConnectionString = _systemOptions.Wiki.Database,
            })
            .WithCustomTextPartitioningOptions(
            new TextPartitioningOptions
            {
                MaxTokensPerParagraph = documentTask.MaxTokensPerParagraph,
                OverlappingTokens = documentTask.OverlappingTokens
            })
            .Build();

        // 先删除
        await memoryClient.DeleteDocumentAsync(documentTask.DocumentId.ToString(), index: documentTask.WikiId.ToString());

        var docs = new Microsoft.KernelMemory.Document()
        {
            Id = documentTask.DocumentId.ToString(),
        };

        docs.AddFile(filePath);
        docs.AddTag("wikiId", documentTask.WikiId.ToString());
        docs.AddTag("fileId", documentFile.FileId.ToString());
        docs.AddTag("fileName", documentFile.FileName);

        try
        {
            var taskId = await memoryClient.ImportDocumentAsync(docs, index: documentTask.WikiId.ToString());
        }
        catch (Exception ex)
        {
            documentTask.State = (int)FileEmbeddingState.Failed;
            documentTask.Message = ex.Message;
            await _databaseContext.SaveChangesAsync();
            throw;
        }

        documentTask.State = (int)FileEmbeddingState.Successful;
        documentTask.Message = "任务处理完成";
        _databaseContext.WikiDocumentTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, EmbeddingDocumentEvent message)
    {
        var documentTask = await _databaseContext.WikiDocumentTasks
             .FirstOrDefaultAsync(x => x.DocumentId == message.DocumentId && x.Id == message.TaskId);

        // 不需要处理
        if (documentTask == null || documentTask.State > (int)FileEmbeddingState.Processing)
        {
            return;
        }

        documentTask.State = (int)FileEmbeddingState.Failed;
        documentTask.Message = ex.Message;

        _databaseContext.WikiDocumentTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync();

        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, EmbeddingDocumentEvent? message, Exception? ex)
    {
        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
        return Task.FromResult(ConsumerState.Ack);
    }
}
