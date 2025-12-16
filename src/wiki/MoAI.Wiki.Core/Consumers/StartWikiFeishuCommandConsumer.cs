#pragma warning disable CA1031 // 不捕获常规异常类型
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Extensions;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using System.Text;

namespace MoAI.Wiki.Consumers;

/// <summary>
/// 爬取页面.
/// </summary>
[Consumer("wiki_feishu_document", Qos = 10)]
public class StartWikiFeishuCommandConsumer : IConsumer<StartWikiFeishuMessage>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly ILogger<StartWikiFeishuCommandConsumer> _logger;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFeishuApiClient _feishuApiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiFeishuCommandConsumer"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="logger"></param>
    /// <param name="messagePublisher"></param>
    /// <param name="serviceProvider"></param>
    public StartWikiFeishuCommandConsumer(IMediator mediator, DatabaseContext databaseContext, SystemOptions systemOptions, ILogger<StartWikiFeishuCommandConsumer> logger, IMessagePublisher messagePublisher, IServiceProvider serviceProvider, IFeishuApiClient feishuApiClient)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _logger = logger;
        _messagePublisher = messagePublisher;
        _serviceProvider = serviceProvider;
        _feishuApiClient = feishuApiClient;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, StartWikiFeishuMessage message)
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
        await ClearFeishuRecordAsync(message.WikiId, message.ConfigId);

        workerTaskEntity.State = (int)WorkerState.Processing;
        workerTaskEntity.Message = "正在爬取网页";
        await UpdateCrawleStateAsync(workerTaskEntity);

        var wikiWebConfig = wikiWebConfigEntity.Config.JsonToObject<WikiFeishuConfig>()!;

        var tenantAccessToken = await _feishuApiClient.GetTenantAccessTokenAsync(new Infra.Feishu.Models.FeishuTenantAccessTokenRequest
        {
            AppId = wikiWebConfig.AppId,
            AppSecret = wikiWebConfig.AppSecret
        });

        if (tenantAccessToken.Code != 0)
        {
            workerTaskEntity.State = (int)WorkerState.Failed;
            workerTaskEntity.Message = $"获取飞书访问令牌失败，({tenantAccessToken.Code}){tenantAccessToken.Msg}";
            await UpdateCrawleStateAsync(workerTaskEntity);
            return;
        }

        var feishuAccessToken = $"Bearer {tenantAccessToken.TenantAccessToken}";

        Queue<WikiNode> nodeTokenQueue = new Queue<WikiNode>();
        HashSet<WikiNode> processedTokens = new HashSet<WikiNode>();

        // 第一层
        await GetWikiNodes(feishuAccessToken, wikiWebConfig.SpaceId, wikiWebConfig.ParentNodeToken, nodeTokenQueue);

        var pageCount = 0;

        // 爬取每一个链接
        while (nodeTokenQueue.Count > 0)
        {
            var currentNode = nodeTokenQueue.Dequeue();

            var pageEntity = new WikiPluginDocumentStateEntity
            {
                WikiId = wikiWebConfigEntity.WikiId,
                ConfigId = wikiWebConfigEntity.Id,
                RelevanceKey = currentNode.NodeToken,
                RelevanceValue = currentNode.ObjToken,
                CreateUserId = workerTaskEntity.CreateUserId,
                UpdateUserId = workerTaskEntity.UpdateUserId,
                Message = "正在处理"
            };

            // 插入页面处理记录
            await _databaseContext.WikiPluginDocumentStates.AddAsync(pageEntity);
            await _databaseContext.SaveChangesAsync();

            try
            {
                var stringBuilder = new StringBuilder();

                // 先读取文档内容
                var wikiPageResponse = await _feishuApiClient.GetDocumentBlocksAsync(feishuAccessToken, currentNode.ObjToken, new GetDocumentBlocksRequest
                {
                    PageSize = 500,
                    PageToken = null
                });

                FeishuWikiToMarkdown.ToMarkdown(stringBuilder, wikiPageResponse.Data.Items);

                _feishuApiClient.CheckCode(wikiPageResponse);

                while (!string.IsNullOrEmpty(wikiPageResponse.Data.PageToken))
                {
                    // 先读取文档内容
                    wikiPageResponse = await _feishuApiClient.GetDocumentBlocksAsync(feishuAccessToken, currentNode.ObjToken, new GetDocumentBlocksRequest
                    {
                        PageSize = 500,
                        PageToken = null
                    });

                    FeishuWikiToMarkdown.ToMarkdown(stringBuilder, wikiPageResponse.Data.Items);

                    _feishuApiClient.CheckCode(wikiPageResponse);
                }

                await SaveContentAsync(pageEntity, $"{currentNode.Title}.md", stringBuilder.ToString());

                // 识别当前文档的子文档
                await GetWikiNodes(feishuAccessToken, wikiWebConfig.SpaceId, currentNode.NodeToken, nodeTokenQueue);

                pageCount++;
                pageEntity.Message = "成功";
            }
            catch (Exception ex)
            {
                pageCount++;
                pageEntity.Message = ex.Message;
                _logger.LogError(ex, "Webpage crawling exception,url:{URL}", currentNode.NodeToken);
            }

            // 更新页面处理状态
            _databaseContext.WikiPluginDocumentStates.Update(pageEntity);
            await _databaseContext.SaveChangesAsync();
            await UpdateCrawleStateAsync(workerTaskEntity);

            workerTaskEntity = await _databaseContext.WorkerTasks
            .FirstOrDefaultAsync(x => x.Id == message.TaskId);

            if (workerTaskEntity == null || workerTaskEntity.State >= (int)WorkerState.Cancal)
            {
                break;
            }
        }

        workerTaskEntity!.State = (int)WorkerState.Successful;
        workerTaskEntity.Message = "爬取完成";
        await UpdateCrawleStateAsync(workerTaskEntity);
    }

    // 获取当前层次的所有子节点
    private async Task GetWikiNodes(string feishuAccessToken, string spaceId, string? nodeToken, Queue<WikiNode> nodes)
    {
        try
        {
            var currentNodes = await _feishuApiClient.GetWikiNodesAsync(feishuAccessToken, spaceId, new GetWikiNodesRequest
            {
                PageSize = 50,
                ParentNodeToken = nodeToken
            });

            _feishuApiClient.CheckCode(currentNodes);

            foreach (var item in currentNodes.Data.Items)
            {
                nodes.Enqueue(item);
            }

            while (!string.IsNullOrEmpty(currentNodes.Data.PageToken))
            {
                currentNodes = await _feishuApiClient.GetWikiNodesAsync(feishuAccessToken, spaceId, new GetWikiNodesRequest
                {
                    PageSize = 50,
                    PageToken = currentNodes.Data.PageToken,
                    ParentNodeToken = nodeToken
                });

                _feishuApiClient.CheckCode(currentNodes);

                foreach (var item in currentNodes.Data.Items)
                {
                    nodes.Enqueue(item);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain the sub-node of the Feishu Knowledge Space. It might be due to insufficient permissions or incorrect configuration.");
        }
    }

    /// <inheritdoc/>
    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWikiFeishuMessage message)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWikiFeishuMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }

    // 更新爬虫状态.
    private async Task UpdateCrawleStateAsync(WorkerTaskEntity crawleTaskEntity)
    {
        _databaseContext.WorkerTasks.Update(crawleTaskEntity);
        await _databaseContext.SaveChangesAsync();
    }

    // 清空该爬虫的文档和向量.
    private async Task ClearFeishuRecordAsync(int wikiId, int configId)
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

    private async Task SaveContentAsync(WikiPluginDocumentStateEntity pageEntity, string fileName, string text)
    {
        // 将文档存储为文件
        var filePath = Path.Combine(Path.GetTempPath(), DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".md");
        await File.WriteAllTextAsync(filePath, text);
        using var streamReader = File.OpenRead(filePath);

        // 计算文件 md5
        var md5 = HashHelper.ComputeFileMd5(filePath);

        // 上传文件
        var uploadResult = await _mediator.Send(new UploadFileStreamCommand
        {
            MD5 = md5,
            ContentType = "text/markdown",
            FileSize = (int)new FileInfo(filePath).Length,
            ObjectKey = $"{pageEntity.WikiId}/{pageEntity.ConfigId}/{md5}.html",
            FileStream = streamReader
        });

        // 插入新的记录
        var wikiDocument = new WikiDocumentEntity
        {
            WikiId = pageEntity.WikiId,
            FileId = uploadResult.FileId,
            FileName = fileName,
            ObjectKey = uploadResult.ObjectKey,
            FileType = ".md",
            IsEmbedding = false
        };

        await _databaseContext.AddRangeAsync(wikiDocument);
        await _databaseContext.SaveChangesAsync();

        pageEntity.WikiDocumentId = wikiDocument.Id;
        _databaseContext.WikiPluginDocumentStates.Update(pageEntity);
        await _databaseContext.SaveChangesAsync();
    }
}