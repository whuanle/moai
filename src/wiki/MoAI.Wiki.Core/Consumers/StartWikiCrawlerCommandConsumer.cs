#pragma warning disable CA1031 // 不捕获常规异常类型
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        // 1，前置检查
        var workerTask = await _databaseContext.WorkerTasks.AsNoTracking()
             .FirstOrDefaultAsync(x => x.Id == message.TaskId);

        // 不需要处理或有其它线程在执行
        if (workerTask == null || workerTask.State > (int)WorkerState.Processing)
        {
            return;
        }

        var wikiWebConfigEntity = await _databaseContext.WikiPluginConfigs
            .FirstOrDefaultAsync(x => x.Id == message.ConfigId && x.WikiId == message.WikiId);

        if (wikiWebConfigEntity == null)
        {
            return;
        }

        var wikiWebConfig = wikiWebConfigEntity.Config.JsonToObject<WikiCrawlerConfig>()!;

        Queue<Url> urls = new Queue<Url>();
        HashSet<string> processedUrls = new HashSet<string>();

        urls.Enqueue(new Url(wikiWebConfig.Address.ToString()));

        // 爬取每一个链接
        while (urls.Count > 0)
        {
            if (processedUrls.Count >= wikiWebConfig.LimitMaxCount)
            {
                break;
            }

            await SetStateAsync(message.TaskId, WorkerState.Processing, "正在爬取页面");

            var currentUrl = urls.Dequeue();

            try
            {
                // 检查这个地址有没有对应的文档
                var overridePage = await CheckExistDocumentAsync(wikiWebConfigEntity.Id, currentUrl, wikiWebConfig.IsIgnoreExistPage);

                if (overridePage.IsOverride)
                {
                    // 先抓取页面，不管本地的数据
                    var uploadResult = await CrawlePageAsync(urls, processedUrls, wikiWebConfig, currentUrl, message.WikiId);

                    // 如果页面没有任何变化，说明网页完全一样，跳过后续处理
                    if (overridePage.WikiDocument != null && overridePage.WikiDocument.FileId == uploadResult.FileId)
                    {
                        continue;
                    }

                    // 新的页面，之前完全没有记录，直接插入
                    if (overridePage.WikiStateDocument == null)
                    {
                        var documentEntity = new WikiDocumentEntity
                        {
                            WikiId = wikiWebConfigEntity.WikiId,
                            FileId = uploadResult.FileId,
                            FileName = uploadResult.FileMd5 + ".html",
                            ObjectKey = uploadResult.ObjectKey,
                            FileType = uploadResult.FileType,
                            IsEmbedding = false,
                        };

                        await _databaseContext.WikiDocuments.AddAsync(documentEntity);
                        await _databaseContext.SaveChangesAsync();

                        var pageEntity = new WikiPluginDocumentStateEntity
                        {
                            WikiId = wikiWebConfigEntity.WikiId,
                            ConfigId = wikiWebConfigEntity.Id,
                            RelevanceKey = currentUrl.ToString(),
                            RelevanceValue = string.Empty,
                            CreateUserId = workerTask.CreateUserId,
                            UpdateUserId = workerTask.UpdateUserId,
                            Message = "正在处理",
                            WikiDocumentId = documentEntity.Id
                        };

                        await _databaseContext.WikiPluginDocumentStates.AddAsync(pageEntity);
                        await _databaseContext.SaveChangesAsync();

                        continue;
                    }

                    // 存在，并且需要替换
                    {
                        // 先删除旧的文件
                        await _mediator.Send(new DeleteFileCommand
                        {
                            FileIds = new[] { overridePage.WikiDocument!.FileId }
                        });

                        var documentEntity = overridePage.WikiDocument!;
                        documentEntity.FileId = uploadResult.FileId;
                        documentEntity.FileName = uploadResult.FileMd5 + ".html";
                        documentEntity.ObjectKey = uploadResult.ObjectKey;
                        documentEntity.FileType = uploadResult.FileType;

                        _databaseContext.WikiDocuments.Update(documentEntity);
                        await _databaseContext.SaveChangesAsync();

                        var pageEntity = overridePage.WikiStateDocument!;
                        pageEntity.WikiDocumentId = documentEntity.Id;
                        _databaseContext.WikiPluginDocumentStates.Update(pageEntity);
                        await _databaseContext.SaveChangesAsync();
                    }

                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Crawler url error:{Url}", currentUrl);
            }
        }

        await SetStateAsync(message.TaskId, WorkerState.Successful, "爬取完成");
    }

    private async Task<(bool IsOverride, WikiPluginDocumentStateEntity? WikiStateDocument, WikiDocumentEntity? WikiDocument)> CheckExistDocumentAsync(int configId, Url currentUrl, bool isIgnore)
    {
        // 查找
        var wikiStateDocument = await _databaseContext.WikiPluginDocumentStates
            .FirstOrDefaultAsync(x => x.ConfigId == configId && x.RelevanceKey == currentUrl.ToString());

        // 如果页面存在，替换还是忽略
        if (wikiStateDocument == null)
        {
            return (true, null, null);
        }

        if (isIgnore)
        {
            return (false, null, null);
        }

        var wikiDocument = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == wikiStateDocument.WikiDocumentId);

        // 不清空向量，只替换文档和删除切割记录
        _databaseContext.WikiPluginDocumentStates.Remove(wikiStateDocument);
        await _databaseContext.SaveChangesAsync();

        return (true, wikiStateDocument, wikiDocument);
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWikiCrawlerMessage? message)
    {
        await SetExceptionStateAsync(message.TaskId, WorkerState.Wait, ex);
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWikiCrawlerMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }

    // 开始爬取一个页面
    private async Task<FileUploadResult> CrawlePageAsync(Queue<Url> urls, HashSet<string> processedUrls, WikiCrawlerConfig wikiWebConfig, Url currentUrl, int wikiId)
    {
        var googleBotUserAgent = wikiWebConfig.UserAgent;
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

        /*
         爬取完成
         */

        // 如果不需要爬取其他地址
        if (string.IsNullOrEmpty(wikiWebConfig.LimitAddress) || currentUrl.ToString().StartsWith(wikiWebConfig.LimitAddress, StringComparison.CurrentCultureIgnoreCase))
        {
            return await SaveWikiCrawlerAsync(wikiWebConfig, currentUrl, document, wikiId);
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

        return await SaveWikiCrawlerAsync(wikiWebConfig, currentUrl, document, wikiId);
    }

    private async Task<FileUploadResult> SaveWikiCrawlerAsync(WikiCrawlerConfig wikiWebConfig, Url currentUrl, IDocument document, int wikiId)
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
        var objectKey = $"{wikiId}/{md5}.html";

        // 检查知识库有没有对应的文件
        // 要考虑一种情况，知识库有对应的文件，但是这个文件不属于这个爬虫任务上传的
        // 所以这个文档可以被多方关联
        var existingFile = await _databaseContext.WikiDocuments.AsNoTracking()
            .Where(x => x.WikiId == wikiId && x.ObjectKey == objectKey)
            .Select(x => new FileUploadResult
            {
                FileId = x.FileId,
                FileMd5 = md5,
                ObjectKey = x.ObjectKey,
                FileType = x.FileType
            })
            .FirstOrDefaultAsync();

        if (existingFile != null)
        {
            // 已经存在该文件，直接返回
            return existingFile;
        }

        // 上传文件
        var uploadResult = await _mediator.Send(new UploadFileStreamCommand
        {
            MD5 = md5,
            ContentType = "text/html",
            FileSize = (int)fileStream.Length,
            ObjectKey = objectKey,
            FileStream = fileStream
        });

        return uploadResult;
    }

    private async Task SetExceptionStateAsync(Guid taskId, WorkerState state, Exception? ex = null)
    {
        _logger.LogError(ex, "Task processing failed.");
        await SetStateAsync(taskId, state, ex?.Message);
    }

    private async Task SetStateAsync(Guid taskId, WorkerState state, string? message = null)
    {
        // 设置之前先检查状态
        var workerTask = await _databaseContext.WorkerTasks
             .FirstOrDefaultAsync(x => x.Id == taskId);

        // 不需要处理或有其它线程在执行
        if (workerTask == null || workerTask.State > (int)WorkerState.Processing)
        {
            throw new BusinessException("当前任务已结束");
        }

        workerTask.State = (int)state;
        if (!string.IsNullOrEmpty(message))
        {
            workerTask.Message = message;
        }
        else
        {
            workerTask.Message = state.ToJsonString();
        }

        _databaseContext.WorkerTasks.Update(workerTask);
        await _databaseContext.SaveChangesAsync();
    }
}