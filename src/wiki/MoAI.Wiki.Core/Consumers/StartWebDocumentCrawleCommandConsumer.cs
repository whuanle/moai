// <copyright file="StartWebDocumentCrawleCommandConsumer.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CA1031 // 不捕获常规异常类型

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAIDocument.Core.Consumers.Events;
using System;

namespace MoAI.Wiki.Consumers;

/// <summary>
/// 爬取页面.
/// </summary>
[Consumer("crawle_document", Qos = 10)]
public class StartWebDocumentCrawleCommandConsumer : IConsumer<StartWebDocumentCrawleMessage>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly ILogger<StartWebDocumentCrawleCommandConsumer> _logger;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWebDocumentCrawleCommandConsumer"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="logger"></param>
    /// <param name="messagePublisher"></param>
    /// <param name="serviceProvider"></param>
    public StartWebDocumentCrawleCommandConsumer(IMediator mediator, DatabaseContext databaseContext, SystemOptions systemOptions, ILogger<StartWebDocumentCrawleCommandConsumer> logger, IMessagePublisher messagePublisher, IServiceProvider serviceProvider)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _logger = logger;
        _messagePublisher = messagePublisher;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, StartWebDocumentCrawleMessage message)
    {
        var crawleTaskEntity = await _databaseContext.WikiWebCrawleTasks
            .FirstOrDefaultAsync(x => x.Id == message.TaskId);

        if (crawleTaskEntity == null || crawleTaskEntity.CrawleState >= (int)CrawleState.Cancal)
        {
            return;
        }

        var wikiWebConfig = await _databaseContext.WikiWebConfigs
            .FirstOrDefaultAsync(x => x.Id == message.WebConfigId && x.WikiId == message.WikiId);

        if (wikiWebConfig == null)
        {
            return;
        }

        crawleTaskEntity.CrawleState = (int)CrawleState.Processing;
        crawleTaskEntity.Message = "网页爬取任务开始执行";
        await UpdateCrawleStateAsync(crawleTaskEntity);

        Queue<Url> urls = new Queue<Url>();
        HashSet<string> processedUrls = new HashSet<string>();

        urls.Enqueue(new Url(wikiWebConfig.Address));

        while (urls.Count > 0)
        {
            var currentUrl = urls.Dequeue();

            var wikiWebCrawlePageStateEntity = new WikiWebCrawlePageStateEntity
            {
                WikiId = wikiWebConfig.WikiId,
                WikiWebConfigId = wikiWebConfig.Id,
                Url = currentUrl.ToString(),
                CreateUserId = crawleTaskEntity.CreateUserId,
                UpdateUserId = crawleTaskEntity.UpdateUserId,
                State = (int)CrawleState.Processing,
                Message = "正在处理"
            };

            // 插入页面处理记录
            await _databaseContext.WikiWebCrawlePageStates.AddAsync(wikiWebCrawlePageStateEntity);
            await _databaseContext.SaveChangesAsync();

            try
            {
                await CrawlePageAsync(urls, processedUrls, wikiWebConfig, crawleTaskEntity, currentUrl);
                crawleTaskEntity.PageCount++;
                wikiWebCrawlePageStateEntity.State = (int)CrawleState.Successful;
                wikiWebCrawlePageStateEntity.Message = "成功";
            }
            catch (Exception ex)
            {
                crawleTaskEntity.FaildPageCount++;
                wikiWebCrawlePageStateEntity.State = (int)CrawleState.Failed;
                wikiWebCrawlePageStateEntity.Message = ex.Message;
                _logger.LogError(ex, "Webpage crawling exception,url:{URL}", currentUrl);
            }

            // 更新页面处理状态
            _databaseContext.WikiWebCrawlePageStates.Update(wikiWebCrawlePageStateEntity);
            await _databaseContext.SaveChangesAsync();
            await UpdateCrawleStateAsync(crawleTaskEntity);

            if (crawleTaskEntity.PageCount + crawleTaskEntity.FaildPageCount >= wikiWebConfig.LimitMaxCount)
            {
                _logger.LogInformation("已达到最大爬取数量，停止爬取，当前数量：{Count}", processedUrls.Count);
                break;
            }
        }

        crawleTaskEntity.CrawleState = (int)CrawleState.Successful;
        crawleTaskEntity.Message = "爬取完成";
        await UpdateCrawleStateAsync(crawleTaskEntity);
    }

    /// <inheritdoc/>
    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWebDocumentCrawleMessage message)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWebDocumentCrawleMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }

    // 更新爬虫状态.
    private async Task UpdateCrawleStateAsync(WikiWebCrawleTaskEntity crawleTaskEntity)
    {
        _databaseContext.WikiWebCrawleTasks.Update(crawleTaskEntity);
        await _databaseContext.SaveChangesAsync();
    }

    // 开始爬取一个页面
    private async Task CrawlePageAsync(Queue<Url> urls, HashSet<string> processedUrls, WikiWebConfigEntity wikiWebConfig, WikiWebCrawleTaskEntity webCrawleTaskEntity, Url currentUrl)
    {
        var config = Configuration.Default
            .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true })
            .WithJs();

        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(currentUrl);

        // 开启不一定有好的效果
        if (wikiWebConfig.IsWaitJs)
        {
            await Task.WhenAll(document.GetScriptDownloads());
        }

        /*
         爬取完成
         */

        if (string.IsNullOrEmpty(wikiWebConfig.LimitAddress) || currentUrl.ToString().StartsWith(wikiWebConfig.LimitAddress, StringComparison.CurrentCultureIgnoreCase))
        {
            await SaveWebDocumentAsync(wikiWebConfig, webCrawleTaskEntity, currentUrl, document);
        }

        var webHost = new Uri(wikiWebConfig.Address);
        var limitWebPath = new Uri(wikiWebConfig.LimitAddress);

        // 如果要爬取页面的链接
        if (wikiWebConfig.IsCrawlOther)
        {
            // 解析文档中的链接
            foreach (var link in document.Links)
            {
                var linkElement = link as IHtmlAnchorElement;
                if (linkElement == null)
                {
                    continue;
                }

                if (linkElement.Href == null || linkElement.Href.StartsWith('#') || linkElement.Href.StartsWith("javascript:", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                // 跳过
                var url = new Url(linkElement.Href);
                if (processedUrls.Contains(url.ToString()))
                {
                    continue;
                }

                if (!url.IsAbsolute)
                {
                    url = new Url(webHost.Host, url.ToString());
                }

                if (!string.IsNullOrEmpty(wikiWebConfig.LimitAddress) && !url.ToString().StartsWith(wikiWebConfig.LimitAddress, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                processedUrls.Add(url.ToString());

                urls.Enqueue(url);
            }
        }
    }

    private async Task SaveWebDocumentAsync(WikiWebConfigEntity wikiWebConfig, WikiWebCrawleTaskEntity webCrawleTaskEntity, Url currentUrl, IDocument document)
    {
        // 删除旧的记录
        await DeleteOldDocumentRecordAsync(wikiWebConfig, currentUrl);

        // 将文档存储为文件
        var filePath = Path.Combine(Path.GetTempPath(), DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".html");
        using TextWriter writer = new StreamWriter(filePath);

        // 对页面内容进行筛选
        if (!string.IsNullOrEmpty(wikiWebConfig.Selector))
        {
            var selectedElements = document.QuerySelectorAll(".content");

            // 复用原网页的 head，替换 body 内容
            var newDocument = document.Clone() as IHtmlDocument;

            if (newDocument != null)
            {
                var newBody = newDocument.Body;

                if (newBody != null)
                {
                    newBody.InnerHtml = string.Empty;

                    foreach (var element in selectedElements)
                    {
                        var clonedElement = element.Clone() as IElement;
                        newBody.AppendChild(clonedElement!);
                    }

                    document = newDocument;
                }
            }
        }

        await document.ToHtmlAsync(writer);
        await writer.FlushAsync();
        await writer.DisposeAsync();

        // 计算文件 md5
        var md5 = HashHelper.ComputeFileMd5(filePath);

        // 上传文件
        var uploadResult = await _mediator.Send(new UploadLocalFileCommand
        {
            Visibility = Store.Enums.FileVisibility.Private,
            File = new FileUploadItem
            {
                MD5 = md5,
                ContentType = "text/html",
                FileName = md5 + ".html",
                FilePath = filePath,
                ObjectKey = $"{wikiWebConfig.WikiId}/{wikiWebConfig.Id}/{md5}.html",
            },
        });

        // 插入新的记录
        var wikiDocument = new WikiDocumentEntity
        {
            WikiId = wikiWebConfig.WikiId,
            FileId = uploadResult.FileId,
            FileName = uploadResult.FileName,
            ObjectKey = uploadResult.ObjectKey,
            FileType = ".html",
            CreateUserId = webCrawleTaskEntity.CreateUserId,
            UpdateUserId = webCrawleTaskEntity.UpdateUserId
        };
        var wikiWebDocument = new WikiWebDocumentEntity
        {
            WikiId = wikiWebConfig.WikiId,
            WikiWebConfigId = wikiWebConfig.Id,
            WikiDocumentId = wikiDocument.Id,
            Url = currentUrl.ToString(),
            CreateUserId = webCrawleTaskEntity.CreateUserId,
            UpdateUserId = webCrawleTaskEntity.UpdateUserId
        };

        await _databaseContext.AddRangeAsync(wikiDocument, wikiWebDocument);
        await _databaseContext.SaveChangesAsync();

        wikiWebDocument.WikiDocumentId = wikiDocument.Id;
        _databaseContext.Update(wikiWebDocument);
        await _databaseContext.SaveChangesAsync();

        if (wikiWebConfig.IsAutoEmbedding)
        {
            var documentTaskEntity = new WikiDocumentTaskEntity
            {
                DocumentId = wikiDocument.Id,
                WikiId = wikiDocument.WikiId,
                State = (int)FileEmbeddingState.Wait,
                MaxTokensPerParagraph = webCrawleTaskEntity.MaxTokensPerParagraph,
                OverlappingTokens = webCrawleTaskEntity.OverlappingTokens,
                Tokenizer = TextToJsonExtensions.ToJsonString(webCrawleTaskEntity.Tokenizer),
                Message = "任务已创建",
                CreateUserId = webCrawleTaskEntity.CreateUserId,
                UpdateUserId = webCrawleTaskEntity.UpdateUserId
            };

            await _databaseContext.WikiDocumentTasks.AddAsync(documentTaskEntity);
            await _databaseContext.SaveChangesAsync();

            // 要向量化
            await _messagePublisher.AutoPublishAsync(new EmbeddingDocumentTaskMesage
            {
                WikiId = wikiWebConfig.WikiId,
                DocumentId = wikiDocument.Id,
                TaskId = documentTaskEntity.Id,
            });
        }
    }

    private async Task DeleteOldDocumentRecordAsync(WikiWebConfigEntity wikiWebConfig, Url currentUrl)
    {
        var oldIds = _databaseContext.WikiWebDocuments.Where(x => x.WikiWebConfigId == wikiWebConfig.Id && x.Url == currentUrl.ToString())
            .Select(x => new
            {
                WikiWebDocumentId = x.Id,
                x.WikiDocumentId
            }).ToArray();

        var webDocumentIds = oldIds.Select(x => x.WikiWebDocumentId).Where(x => x != 0).ToHashSet();

        // 删除数据库数据
        if (webDocumentIds.Count > 0)
        {
            await _databaseContext.SoftDeleteAsync(_databaseContext.WikiWebDocuments.Where(x => webDocumentIds.Contains(x.Id)));
        }

        var documentIds = oldIds.Select(x => x.WikiDocumentId).Where(x => x != 0).ToHashSet();

        if (documentIds.Count > 0)
        {
            await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocuments.Where(x => documentIds.Contains(x.Id)));
        }

        var memoryDb = _serviceProvider.GetKeyedService<IMemoryDbClient>(_systemOptions.Wiki.DBType);
        if (memoryDb == null)
        {
            throw new BusinessException("不支持的文档数据库");
        }

        // 删除知识库文档
        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder()
            .WithSimpleFileStorage(Path.GetTempPath());

        memoryBuilder = memoryDb.Configure(memoryBuilder, _systemOptions.Wiki.ConnectionString);

        var memoryClient = memoryBuilder
            .WithoutTextGenerator()
            .WithoutEmbeddingGenerator()
            .Build();

        // 删除知识库文档
        foreach (var documentId in documentIds)
        {
            // 删除文档
            await memoryClient.DeleteDocumentAsync(documentId.ToString(), index: wikiWebConfig.WikiId.ToString());
        }
    }
}