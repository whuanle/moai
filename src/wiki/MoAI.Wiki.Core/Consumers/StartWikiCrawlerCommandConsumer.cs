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
using MoAI.Infra.Extensions;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Wiki.Batch.Commands;
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
        var wikiWebConfigEntity = await _databaseContext.WikiPluginConfigs.AsNoTracking()
            .Where(x => x.Id == message.ConfigId && x.WikiId == message.WikiId)
            .Select(x => new
            {
                x.Id,
                x.Config,
                x.WorkState,
                x.WikiId,
                x.UpdateUserId
            })
            .FirstOrDefaultAsync();

        if (wikiWebConfigEntity == null)
        {
            return;
        }

        // 任务已经结束了
        if (wikiWebConfigEntity.WorkState > (int)WorkerState.Processing)
        {
            return;
        }

        var (isBreak, _) = await SetStateAsync(message.ConfigId, WorkerState.Processing, "正在爬取页面");
        if (isBreak)
        {
            return;
        }

        // 清空所有页面爬取记录
        await _databaseContext.WikiPluginConfigDocumentStates.Where(x => x.WikiId == message.WikiId && x.ConfigId == message.ConfigId).ExecuteDeleteAsync();

        var wikiWebConfig = wikiWebConfigEntity.Config.JsonToObject<WikiCrawlerConfig>()!;

        // 准备爬取的列表
        Queue<Url> workUrls = new Queue<Url>();

        // 已经爬取过的列表或者被忽略的页面
        HashSet<string> existUrl = new HashSet<string>();

        // 新页面数量
        int newPageCount = 0;

        workUrls.Enqueue(new Url(wikiWebConfig.Address.ToString()));

        var maxNewCount = wikiWebConfig.LimitMaxNewCount;
        if (maxNewCount == 0)
        {
            maxNewCount = 1000;
        }

        // 爬取每一个链接
        while (workUrls.Count > 0 && newPageCount < maxNewCount)
        {
            _databaseContext.ChangeTracker.Clear();

            // 每次爬取之前检查状态
            (isBreak, _) = await SetStateAsync(message.ConfigId, WorkerState.Processing, "正在爬取页面");
            if (isBreak)
            {
                return;
            }

            var currentUrl = workUrls.Dequeue();

            try
            {
                // 检查这个地址有没有对应的文档
                var overridePage = await CheckExistDocumentAsync(message.ConfigId, currentUrl, !wikiWebConfig.IsOverExistPage);

                if (!overridePage.IsNext)
                {
                    await SetPageAsync(message.WikiId, message.ConfigId, currentUrl, WorkerState.Cancal);
                    continue;
                }

                // 先抓取页面，保存的是对应的文件，但是不涉及业务
                var uploadResult = await CrawlePageAsync(workUrls, existUrl, wikiWebConfig, currentUrl, message.WikiId, message.ConfigId);

                // 如果页面没有任何变化，说明网页完全一样，跳过后续处理
                if (overridePage.WikiDocument != null && overridePage.WikiDocument.FileId == uploadResult.FileId)
                {
                    await SetPageAsync(message.WikiId, message.ConfigId, currentUrl, WorkerState.Cancal);
                    continue;
                }

                // 新的页面，之前完全没有记录，直接插入
                if (overridePage.WikiConfigDocument == null)
                {
                    // 片段这个文件是否被其他配置使用，或者被当前配置的其他文档使用了
                    var existingDocument = await _databaseContext.WikiDocuments.AsNoTracking()
                        .Where(x => x.WikiId == message.WikiId && x.FileId == uploadResult.FileId)
                        .FirstOrDefaultAsync();

                    // 主要被其它地方使用了，都忽略本次操作
                    if (existingDocument != null)
                    {
                        await SetPageAsync(message.WikiId, message.ConfigId, currentUrl, WorkerState.Cancal);
                        continue;
                    }

                    newPageCount++;

                    var documentEntity = new WikiDocumentEntity
                    {
                        WikiId = wikiWebConfigEntity.WikiId,
                        FileId = uploadResult.FileId,
                        FileName = uploadResult.FileMd5 + ".html",
                        ObjectKey = uploadResult.ObjectKey,
                        FileType = ".html",
                        IsEmbedding = false,
                    };

                    await _databaseContext.WikiDocuments.AddAsync(documentEntity);
                    await _databaseContext.SaveChangesAsync();

                    var pageEntity = new WikiPluginConfigDocumentEntity
                    {
                        WikiId = wikiWebConfigEntity.WikiId,
                        ConfigId = wikiWebConfigEntity.Id,
                        RelevanceKey = "url",
                        RelevanceValue = currentUrl.ToString(),
                        CreateUserId = wikiWebConfigEntity.UpdateUserId,
                        UpdateUserId = wikiWebConfigEntity.UpdateUserId,
                        WikiDocumentId = documentEntity.Id
                    };

                    await _databaseContext.WikiPluginConfigDocuments.AddAsync(pageEntity);
                    await _databaseContext.SaveChangesAsync();

                    await SetPageAsync(message.WikiId, message.ConfigId, currentUrl, WorkerState.Successful);
                    continue;
                }

                // 记录存在，重新爬取后，碰到页面有没有变化
                if (overridePage.WikiDocument != null)
                {
                    if (overridePage.WikiDocument.FileId == uploadResult.FileId)
                    {
                        await SetPageAsync(message.WikiId, configId: message.ConfigId, currentUrl, WorkerState.Cancal);
                        continue;
                    }
                }

                // 存在，并且需要覆盖，因为抓取的页面有变化
                {
                    // 片段这个文件是否被其他配置使用，或者被当前配置的其他文档使用了
                    var existingDocument = await _databaseContext.WikiDocuments.AsNoTracking()
                        .Where(x => x.WikiId == message.WikiId && x.FileId == uploadResult.FileId)
                        .FirstOrDefaultAsync();

                    // 主要被其它地方使用了，都忽略本次操作
                    if (existingDocument != null)
                    {
                        await SetPageAsync(message.WikiId, configId: message.ConfigId, currentUrl, WorkerState.Cancal);
                        continue;
                    }

                    // 先删除旧的文件
                    await _mediator.Send(new DeleteFileCommand
                    {
                        FileIds = new[] { overridePage.WikiDocument!.FileId }
                    });

                    var documentEntity = overridePage.WikiDocument!;
                    documentEntity.FileId = uploadResult.FileId;
                    documentEntity.FileName = uploadResult.FileMd5 + ".html";
                    documentEntity.ObjectKey = uploadResult.ObjectKey;
                    documentEntity.FileType = ".html";

                    _databaseContext.WikiDocuments.Update(documentEntity);
                    await _databaseContext.SaveChangesAsync();

                    var pageEntity = overridePage.WikiConfigDocument!;
                    pageEntity.WikiDocumentId = documentEntity.Id;
                    _databaseContext.WikiPluginConfigDocuments.Update(pageEntity);
                    await _databaseContext.SaveChangesAsync();

                    await SetPageAsync(message.WikiId, message.ConfigId, currentUrl, WorkerState.Successful);
                }

                continue;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Crawler url error:{Url}", currentUrl);
                await SetPageAsync(message.WikiId, message.ConfigId, currentUrl, WorkerState.Failed, ex);
            }
        }

        // 将那些没有爬取的任务设置为已取消
        await _databaseContext.WhereUpdateAsync(
            _databaseContext.WikiPluginConfigDocumentStates.Where(x => x.WikiId == message.WikiId && x.ConfigId == message.ConfigId && x.State == (int)WorkerState.Wait),
            x => x.SetProperty(a => a.State, (int)WorkerState.Cancal));

        (isBreak, _) = await SetStateAsync(wikiWebConfigEntity.Id, WorkerState.Successful, "爬取完成");
        if (isBreak)
        {
            return;
        }

        try
        {
            if (message.Command != null && message.Command.IsAutoProcess && message.Command.AutoProcessConfig != null)
            {
                var documentIds = await _databaseContext.WikiPluginConfigDocuments.AsNoTracking()
                    .Where(x => x.WikiId == message.WikiId && x.ConfigId == message.ConfigId)
                    .Select(x => x.WikiDocumentId)
                    .ToListAsync();

                await _mediator.Send(new WikiBatchProcessDocumentCommand
                {
                    WikiId = message.WikiId,
                    AiPartion = message.Command.AutoProcessConfig.AiPartion,
                    IsEmbedSourceText = message.Command.AutoProcessConfig.IsEmbedSourceText ?? false,
                    Partion = message.Command.AutoProcessConfig.Partion,
                    PreprocessStrategyType = message.Command.AutoProcessConfig.PreprocessStrategyType,
                    ThreadCount = message.Command.AutoProcessConfig.ThreadCount,
                    DocumentIds = documentIds,
                    PreprocessStrategyAiModel = message.Command.AutoProcessConfig.PreprocessStrategyAiModel ?? 0,
                    IsEmbedding = message.Command.AutoProcessConfig.IsEmbedding ?? false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Can't start WikiBatchProcessDocumentCommand");
        }
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWikiCrawlerMessage? message)
    {
        await SetExceptionStateAsync(message!.ConfigId, WorkerState.Failed, ex);
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWikiCrawlerMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }

    private async Task SetPageAsync(int wikiId, int configId, Url currentUrl, WorkerState? workerState = null, Exception? ex = null)
    {
        var existState = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WikiId == wikiId && x.ConfigId == configId && x.RelevanceValue == currentUrl.ToString());

        if (workerState == null)
        {
            workerState = WorkerState.Wait;
        }

        string message = string.Empty;
        switch (workerState)
        {
            case WorkerState.Wait:
                message = "等待爬取";
                break;
            case WorkerState.Processing:
                message = "正在爬取";
                break;
            case WorkerState.Successful:
                message = "爬取成功";
                break;
            case WorkerState.Failed:
                message = "爬取失败";
                break;
            case WorkerState.Cancal:
                message = "爬取取消";
                break;
        }

        if (ex != null)
        {
            message = ex.Message;
        }

        if (existState == null)
        {
            await _databaseContext.WikiPluginConfigDocumentStates.AddAsync(new WikiPluginConfigDocumentStateEntity
            {
                WikiId = wikiId,
                Message = message,
                RelevanceKey = "url",
                RelevanceValue = currentUrl.ToString(),
                ConfigId = configId,
                State = (int)workerState,
            });

            await _databaseContext.SaveChangesAsync();

            _databaseContext.ChangeTracker.Clear();
            return;
        }

        existState.State = (int)workerState;
        existState.Message = message;

        var newState = _databaseContext.WikiPluginConfigDocumentStates.Update(existState);
        await _databaseContext.SaveChangesAsync();
        _databaseContext.ChangeTracker.Clear();
    }

    private async Task SetPageAsync(int wikiId, int configId, List<string> urls, WorkerState? workerState = null, Exception? ex = null)
    {
        if (urls == null || urls.Count == 0)
        {
            return;
        }

        workerState ??= WorkerState.Wait;

        string message = workerState switch
        {
            WorkerState.Wait => "等待爬取",
            WorkerState.Processing => "正在爬取",
            WorkerState.Successful => "爬取成功",
            WorkerState.Failed => "爬取失败",
            WorkerState.Cancal => "爬取取消",
            _ => string.Empty
        };

        if (ex != null)
        {
            message = ex.Message;
        }

        var targetUrls = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct()
            .ToList();

        if (targetUrls.Count == 0)
        {
            return;
        }

        var existingUrls = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
            .Where(x => x.WikiId == wikiId && x.ConfigId == configId && targetUrls.Contains(x.RelevanceValue))
            .Select(x => x.RelevanceValue)
            .ToListAsync();

        var newUrls = targetUrls.Except(existingUrls).ToList();
        if (newUrls.Count == 0)
        {
            return;
        }

        var entities = newUrls.Select(url => new WikiPluginConfigDocumentStateEntity
        {
            WikiId = wikiId,
            Message = message,
            RelevanceKey = "url",
            RelevanceValue = url,
            ConfigId = configId,
            State = (int)workerState,
        });

        await _databaseContext.WikiPluginConfigDocumentStates.AddRangeAsync(entities);
        await _databaseContext.SaveChangesAsync();
        _databaseContext.ChangeTracker.Clear();
    }

    private async Task<(bool IsNext, WikiPluginConfigDocumentEntity? WikiConfigDocument, WikiDocumentEntity? WikiDocument)> CheckExistDocumentAsync(int configId, Url currentUrl, bool isIgnore)
    {
        // 查找
        var wikiConfigDocument = await _databaseContext.WikiPluginConfigDocuments
            .FirstOrDefaultAsync(x => x.ConfigId == configId && x.RelevanceValue == currentUrl.ToString());

        // 如果页面存在，替换还是忽略
        if (wikiConfigDocument == null)
        {
            return (true, null, null);
        }

        if (isIgnore)
        {
            return (false, null, null);
        }

        // 如果要覆盖
        var wikiDocument = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == wikiConfigDocument.WikiDocumentId);

        // 记录错漏
        if (wikiDocument == null)
        {
            _databaseContext.WikiPluginConfigDocuments.Remove(wikiConfigDocument);
            await _databaseContext.SaveChangesAsync();

            return (true, null, null);
        }

        return (true, wikiConfigDocument, wikiDocument);
    }

    // 开始爬取一个页面
    private async Task<FileUploadResult> CrawlePageAsync(Queue<Url> workUrls, HashSet<string> existUrls, WikiCrawlerConfig wikiWebConfig, Url currentUrl, int wikiId, int configId)
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
        if (wikiWebConfig.IsCrawlOther == false || string.IsNullOrEmpty(wikiWebConfig.LimitAddress) || !currentUrl.ToString().StartsWith(wikiWebConfig.LimitAddress, StringComparison.CurrentCultureIgnoreCase))
        {
            return await SaveWikiCrawlerAsync(wikiWebConfig, currentUrl, document, wikiId);
        }

        var webHost = new Uri(wikiWebConfig.Address);
        var limitWebPath = new Uri(wikiWebConfig.LimitAddress);

        var newUrls = new List<string>();

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
                if (existUrls.Contains(url.ToString()))
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

                existUrls.Add(url.ToString());

                // 不能超过 N 个任务
                if (existUrls.Count + workUrls.Count < wikiWebConfig.LimitMaxCount)
                {
                    workUrls.Enqueue(url);
                    newUrls.Add(url.ToString());
                }
            }
        }

        await SetPageAsync(wikiId, configId, newUrls, WorkerState.Wait);

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
        await fileStream.FlushAsync();
        fileStream.Seek(0, SeekOrigin.Begin);
        await writer.DisposeAsync();
        await fileStream.DisposeAsync();

        using var htmlStream = File.OpenRead(filePath);

        // 计算文件 md5
        var md5 = HashHelper.ComputeFileMd5(filePath);
        var objectKey = $"{wikiId}/{md5}.html";

        // 上传文件
        var uploadResult = await _mediator.Send(new UploadFileStreamCommand
        {
            MD5 = md5,
            ContentType = "text/html",
            FileSize = (int)htmlStream.Length,
            ObjectKey = objectKey,
            FileStream = htmlStream
        });

        return uploadResult;
    }

    private async Task<(bool IsBreak, WorkerState WorkerState)> SetExceptionStateAsync(int wikiConfigId, WorkerState state, Exception? ex = null)
    {
        _logger.LogError(ex, "Task processing failed.");
        return await SetStateAsync(wikiConfigId, state, ex?.Message);
    }

    private async Task<(bool IsBreak, WorkerState WorkerState)> SetStateAsync(int wikiConfigId, WorkerState state, string? message = null)
    {
        _databaseContext.ChangeTracker.Clear();

        // 设置之前先检查状态
        var wikiWebConfigEntity = await _databaseContext.WikiPluginConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == wikiConfigId);

        if (wikiWebConfigEntity == null)
        {
            return (true, WorkerState.Cancal);
        }

        // 不需要处理或有其它线程在执行
        if (wikiWebConfigEntity.WorkState > (int)WorkerState.Processing)
        {
            return (true, WorkerState.Cancal);
        }

        wikiWebConfigEntity.WorkState = (int)state;
        if (!string.IsNullOrEmpty(message))
        {
            wikiWebConfigEntity.WorkMessage = message;
        }
        else
        {
            wikiWebConfigEntity.WorkMessage = state.ToJsonString();
        }

        var entityState = _databaseContext.WikiPluginConfigs.Update(wikiWebConfigEntity);
        await _databaseContext.SaveChangesAsync();
        _databaseContext.ChangeTracker.Clear();

        return (false, state);
    }
}