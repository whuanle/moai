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
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Consumers;

/// <summary>
/// 爬取页面.
/// </summary>
[Consumer("wiki_crawle_document", Qos = 10)]
public class StartWikiCrawlerCommandConsumer : IConsumer<StartWikiCrawlerMessage>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly ILogger<StartWikiCrawlerCommandConsumer> _logger;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiCrawlerCommandConsumer"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="logger"></param>
    /// <param name="messagePublisher"></param>
    /// <param name="serviceProvider"></param>
    public StartWikiCrawlerCommandConsumer(IMediator mediator, DatabaseContext databaseContext, SystemOptions systemOptions, ILogger<StartWikiCrawlerCommandConsumer> logger, IMessagePublisher messagePublisher, IServiceProvider serviceProvider)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _logger = logger;
        _messagePublisher = messagePublisher;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, StartWikiCrawlerMessage message)
    {
        var workerTaskEntity = await _databaseContext.WorkerTasks
            .FirstOrDefaultAsync(x => x.Id == message.TaskId);

        if (workerTaskEntity == null || workerTaskEntity.State >= (int)WorkerState.Cancal)
        {
            return;
        }

        var wikiWebConfigEntity = await _databaseContext.WikiPluginConfigs
            .FirstOrDefaultAsync(x => x.Id == message.ConfigId && x.WikiId == message.WikiId);

        if (wikiWebConfigEntity == null)
        {
            return;
        }

        // 清空当前任务记录
        // 删除关联文档
        // 删除向量
        // 删除索引
        await ClearCrawlerRecordAsync(message.WikiId, message.ConfigId);

        workerTaskEntity.State = (int)WorkerState.Processing;
        workerTaskEntity.Message = "正在爬取网页";
        await UpdateCrawleStateAsync(workerTaskEntity);

        var wikiWebConfig = wikiWebConfigEntity.Config.JsonToObject<WikiCrawlerConfig>()!;

        Queue<Url> urls = new Queue<Url>();
        HashSet<string> processedUrls = new HashSet<string>();

        urls.Enqueue(new Url(wikiWebConfig.Address.ToString()));

        var pageCount = 0;

        // 爬取每一个链接
        while (urls.Count > 0)
        {
            var currentUrl = urls.Dequeue();

            var pageEntity = new WikiPluginDocumentStateEntity
            {
                WikiId = wikiWebConfigEntity.WikiId,
                ConfigId = wikiWebConfigEntity.Id,
                RelevanceKey = currentUrl.ToString(),
                RelevanceValue = string.Empty,
                CreateUserId = workerTaskEntity.CreateUserId,
                UpdateUserId = workerTaskEntity.UpdateUserId,
                State = (int)WorkerState.Processing,
                Message = "正在处理"
            };

            // 插入页面处理记录
            await _databaseContext.WikiPluginDocumentStates.AddAsync(pageEntity);
            await _databaseContext.SaveChangesAsync();

            try
            {
                await CrawlePageAsync(urls, processedUrls, pageEntity, wikiWebConfig, currentUrl);
                pageCount++;
                pageEntity.State = (int)WorkerState.Successful;
                pageEntity.Message = "成功";
            }
            catch (Exception ex)
            {
                pageCount++;
                pageEntity.State = (int)WorkerState.Failed;
                pageEntity.Message = ex.Message;
                _logger.LogError(ex, "Webpage crawling exception,url:{URL}", currentUrl);
            }

            // 更新页面处理状态
            _databaseContext.WikiPluginDocumentStates.Update(pageEntity);
            await _databaseContext.SaveChangesAsync();
            await UpdateCrawleStateAsync(workerTaskEntity);

            if (pageCount >= wikiWebConfig.LimitMaxCount)
            {
                _logger.LogInformation("已达到最大爬取数量，停止爬取，当前数量：{Count}", processedUrls.Count);
                break;
            }

            workerTaskEntity = await _databaseContext.WorkerTasks
            .FirstOrDefaultAsync(x => x.Id == message.TaskId);

            if (workerTaskEntity == null || workerTaskEntity.State >= (int)WorkerState.Cancal)
            {
                _logger.LogInformation("任务已取消，停止爬取，当前数量：{Count}", processedUrls.Count);
                break;
            }
        }

        workerTaskEntity!.State = (int)WorkerState.Successful;
        workerTaskEntity.Message = "爬取完成";
        await UpdateCrawleStateAsync(workerTaskEntity);
    }

    /// <inheritdoc/>
    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWikiCrawlerMessage message)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWikiCrawlerMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }

    // 更新爬虫状态.
    private async Task UpdateCrawleStateAsync(WorkerTaskEntity crawleTaskEntity)
    {
        _databaseContext.WorkerTasks.Update(crawleTaskEntity);
        await _databaseContext.SaveChangesAsync();
    }

    // 开始爬取一个页面
    private async Task CrawlePageAsync(Queue<Url> urls, HashSet<string> processedUrls, WikiPluginDocumentStateEntity pageEntity, WikiCrawlerConfig wikiWebConfig, Url currentUrl)
    {
        var googleBotUserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
        var requester = new DefaultHttpRequester(googleBotUserAgent);

        // 避免抓取视频和图片等大资源
        var config = Configuration.Default
            .WithDefaultLoader(new LoaderOptions
            {
                IsResourceLoadingEnabled = false,
                Filter = request =>
                {
                    // 只抓取 html 页面
                    return true;
                }
            }).WithDefaultCookies()
            .With(requester);

        using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(wikiWebConfig.TimeOutSecond));

        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(currentUrl, cancellationTokenSource.Token);

        //// 开启不一定有好的效果
        //if (wikiWebConfig.IsWaitJs)
        //{
        //    await Task.WhenAll(document.GetScriptDownloads());
        //}

        /*
         爬取完成
         */

        if (string.IsNullOrEmpty(wikiWebConfig.LimitAddress) || currentUrl.ToString().StartsWith(wikiWebConfig.LimitAddress, StringComparison.CurrentCultureIgnoreCase))
        {
            await SaveWikiCrawlerAsync(pageEntity, wikiWebConfig, currentUrl, document);
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

    private async Task SaveWikiCrawlerAsync(WikiPluginDocumentStateEntity pageEntity, WikiCrawlerConfig wikiWebConfig, Url currentUrl, IDocument document)
    {
        // 将文档存储为文件
        var filePath = Path.Combine(Path.GetTempPath(), DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".html");
        using var fileStream = File.Create(filePath);
        using TextWriter writer = new StreamWriter(fileStream);

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
        await fileStream.FlushAsync();
        fileStream.Seek(0, SeekOrigin.Begin);

        // 计算文件 md5
        var md5 = HashHelper.ComputeFileMd5(filePath);

        // 上传文件
        var uploadResult = await _mediator.Send(new UploadFileStreamCommand
        {
            MD5 = md5,
            ContentType = "text/html",
            FileSize = (int)fileStream.Length,
            ObjectKey = $"{pageEntity.WikiId}/{pageEntity.ConfigId}/{md5}.html",
            FileStream = fileStream
        });

        // 插入新的记录
        var wikiDocument = new WikiDocumentEntity
        {
            WikiId = pageEntity.WikiId,
            FileId = uploadResult.FileId,
            FileName = $"{md5}.html",
            ObjectKey = uploadResult.ObjectKey,
            FileType = ".html",
            IsEmbedding = false
        };

        await _databaseContext.AddRangeAsync(wikiDocument);
        await _databaseContext.SaveChangesAsync();

        pageEntity.WikiDocumentId = wikiDocument.Id;
        _databaseContext.WikiPluginDocumentStates.Update(pageEntity);
        await _databaseContext.SaveChangesAsync();

        //if (wikiWebConfig.IsAutoEmbedding)
        //{
        //    var documentTaskEntity = new WorkerTaskEntity
        //    {
        //        DocumentId = wikiDocument.Id,
        //        WikiId = wikiDocument.WikiId,
        //        State = (int)FileEmbeddingState.Wait,
        //        MaxTokensPerParagraph = webCrawleTaskEntity.MaxTokensPerParagraph,
        //        OverlappingTokens = webCrawleTaskEntity.OverlappingTokens,
        //        Tokenizer = TextToJsonExtensions.ToJsonString(webCrawleTaskEntity.Tokenizer),
        //        Message = "任务已创建",
        //        CreateUserId = webCrawleTaskEntity.CreateUserId,
        //        UpdateUserId = webCrawleTaskEntity.UpdateUserId
        //    };

        //    await _databaseContext.WikiDocumentTasks.AddAsync(documentTaskEntity);
        //    await _databaseContext.SaveChangesAsync();
        //}
    }

    // 清空该爬虫的文档和向量.
    private async Task ClearCrawlerRecordAsync(int wikiId, int configId)
    {
        var oldIds = _databaseContext.WikiPluginDocumentStates.Where(x => x.ConfigId == configId)
            .Select(x => new
            {
                PageId = x.Id,
                x.WikiDocumentId
            }).ToArray();

        if (oldIds.Length == 0)
        {
            return;
        }

        var documentIds = oldIds.Select(x => x.WikiDocumentId).Where(x => x != 0).ToHashSet();

        // 删除数据库数据
        await _databaseContext.SoftDeleteAsync(_databaseContext.Files.Where(x => _databaseContext.WikiDocuments.Where(a => documentIds.Contains(x.Id) && a.FileId == x.Id).Any()));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocuments.Where(x => _databaseContext.WikiPluginDocumentStates.Where(a => a.ConfigId == configId && a.WikiDocumentId == x.Id).Any()));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiPluginDocumentStates.Where(x => x.ConfigId == configId));

        //var memoryDb = _serviceProvider.GetKeyedService<IMemoryDbClient>(_systemOptions.Wiki.DBType);
        //if (memoryDb == null)
        //{
        //    throw new BusinessException("不支持的文档数据库");
        //}

        //// 删除知识库文档
        //// 构建客户端
        //var memoryBuilder = new KernelMemoryBuilder()
        //    .WithSimpleFileStorage(Path.GetTempPath());

        //memoryBuilder = memoryDb.Configure(memoryBuilder, _systemOptions.Wiki.ConnectionString);

        //var memoryClient = memoryBuilder
        //    .WithoutTextGenerator()
        //    .WithoutEmbeddingGenerator()
        //    .Build();

        //// 删除向量
        //foreach (var documentId in documentIds)
        //{
        //    await memoryClient.DeleteDocumentAsync(documentId.ToString(), index: wikiId.ToString());
        //}
    }
}