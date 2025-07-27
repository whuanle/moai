// <copyright file="StartWebDocumentCrawleCommandConsumer.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using Maomi.MQ;
using MaomiAI.Document.Core.Consumers.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWebDocumentCrawleCommandConsumer"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    public StartWebDocumentCrawleCommandConsumer(IMediator mediator, DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, StartWebDocumentCrawleMessage message)
    {
        var crawleTaskEntity = await _databaseContext.WikiWebCrawleTasks
            .FirstOrDefaultAsync(x => x.WikiId == message.WikiId && x.WikiWebConfigId == message.WebConfigId);

        if (crawleTaskEntity == null || crawleTaskEntity.CrawleState >= (int)CrawleState.Cancal)
        {
            return;
        }

        var webDocumentConfig = await _databaseContext.WikiWebConfigs
            .FirstOrDefaultAsync(x => x.Id == message.WebConfigId && x.WikiId == message.WikiId);

        if (webDocumentConfig == null)
        {
            crawleTaskEntity.CrawleState = (int)CrawleState.Failed;
            crawleTaskEntity.Message = "网页爬取配置不存在";

            _databaseContext.WikiWebCrawleTasks.Update(crawleTaskEntity);
            await _databaseContext.SaveChangesAsync();
            return;
        }

        crawleTaskEntity.CrawleState = (int)CrawleState.Processing;
        crawleTaskEntity.Message = "网页爬取任务开始执行";
        _databaseContext.WikiWebCrawleTasks.Update(crawleTaskEntity);
        await _databaseContext.SaveChangesAsync();

        Queue<Url> urls = new Queue<Url>();
        HashSet<string> processedUrls = new HashSet<string>();

        urls.Enqueue(new Url(webDocumentConfig.Address));

        while (urls.Count > 0)
        {
            await CrawlePageAsync(urls, processedUrls, webDocumentConfig, urls.Dequeue());
        }

    }

    private async Task CrawlePageAsync(Queue<Url> urls, HashSet<string> processedUrls, WikiWebConfigEntity wikiWebConfig, Url currentUrl)
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

        // 将文档存储为文件
        var filePath = Path.Combine(Path.GetTempPath(), DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".html");
        using TextWriter writer = new StreamWriter(filePath);
        await document.ToHtmlAsync(writer);
        await writer.FlushAsync();
        await writer.DisposeAsync();

        // 计算文件 md5
        var md5 = HashHelper.ComputeSha256Hash(filePath);

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
        };
        var wikiWebDocument = new WikiWebDocumentEntity
        {
            WikiId = wikiWebConfig.WikiId,
            WikiWebConfigId = wikiWebConfig.Id,
            WikiDocumentId = wikiDocument.Id,
            Url = currentUrl.ToString(),
        };

        await _databaseContext.AddRangeAsync(wikiDocument, wikiWebDocument);
        await _databaseContext.SaveChangesAsync();

        // 删除旧的记录
        await DeleteOldDocumentRecordAsync(wikiWebConfig, currentUrl);

        if (wikiWebConfig.IsAutoEmbedding)
        {
            // 要向量化
            await _mediator.Publish(new EmbeddingDocumentTaskMesage
            {
                WikiId = wikiWebConfig.WikiId,
                DocumentId = wikiDocument.Id,
                //TaskId = Guid.CreateVersion7().ToString(),
            });
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



                processedUrls.Add(url.ToString());

                urls.Enqueue(url);
            }
        }
    }

    private async Task DeleteOldDocumentRecordAsync(WikiWebConfigEntity wikiWebConfig, Url currentUrl)
    {
        var oldIds = _databaseContext.WikiWebDocuments.Where(x => x.WikiId == wikiWebConfig.WikiId && x.WikiWebConfigId == wikiWebConfig.Id && x.Url == currentUrl.ToString()).Select(x => new
        {
            WikiWebDocumentId = x.Id,
            x.WikiDocumentId
        }).ToArray();

        var webDocumentIds = oldIds.Select(x => x.WikiDocumentId).ToArray();
        var documentIds = _databaseContext.WikiDocuments.Where(x => webDocumentIds.Contains(x.Id)).Select(x => x.Id).ToArray();

        // 删除数据库数据
        if (webDocumentIds.Length > 0)
        {
            await _databaseContext.SoftDeleteAsync(_databaseContext.WikiWebDocuments.Where(x => webDocumentIds.Contains(x.Id)));
        }

        if (documentIds.Length > 0)
        {
            await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocuments.Where(x => documentIds.Contains(x.Id)));
        }

        // 删除知识库文档
        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder()
            .WithSimpleFileStorage(Path.GetTempPath());
        var memoryClient = memoryBuilder
            .WithoutTextGenerator()
            .WithoutEmbeddingGenerator()
            .WithPostgresMemoryDb(new PostgresConfig
            {
                ConnectionString = _systemOptions.Wiki.Database,
            })
            .Build();

        // 删除知识库文档
        foreach (var documentId in documentIds)
        {
            // 删除文档
            await memoryClient.DeleteDocumentAsync(documentId.ToString(), index: wikiWebConfig.WikiId.ToString());
        }
    }

    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWebDocumentCrawleMessage message)
    {
        return Task.CompletedTask;
    }

    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWebDocumentCrawleMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }
}