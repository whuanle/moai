#pragma warning disable CA1031 // 不捕获常规异常类型
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.Helpers;
using MoAI.Storage.Commands;
using MoAI.Wiki.Batch.Commands;
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
    private const int MaxFileNameLength = 120;

    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly ILogger<StartWikiFeishuCommandConsumer> _logger;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFeishuApiClient _feishuApiClient;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiFeishuCommandConsumer"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="logger"></param>
    /// <param name="messagePublisher"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="feishuApiClient"></param>
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

        var wikiWebConfig = wikiWebConfigEntity.Config.JsonToObject<WikiFeishuConfig>()!;

        var tenantAccessToken = await _feishuApiClient.GetTenantAccessTokenAsync(new Infra.Feishu.Models.FeishuTenantAccessTokenRequest
        {
            AppId = wikiWebConfig.AppId,
            AppSecret = wikiWebConfig.AppSecret
        });

        if (tenantAccessToken.Code != 0)
        {
            await SetExceptionStateAsync(message.ConfigId, WorkerState.Failed, new BusinessException($"获取飞书访问令牌失败，({tenantAccessToken.Code}){tenantAccessToken.Msg}"));
            return;
        }

        var feishuAccessToken = $"Bearer {tenantAccessToken.TenantAccessToken}";

        Queue<WikiNode> workNodes = new Queue<WikiNode>();

        if (string.IsNullOrEmpty(wikiWebConfig.ParentNodeToken))
        {
            // 不填写具体节点时，自动从根开始遍历第一层
            await GetWikiNodes(message.WikiId, message.ConfigId, feishuAccessToken, wikiWebConfig.SpaceId, wikiWebConfig.ParentNodeToken, workNodes);
        }
        else
        {
            // 获取这个节点的信息
            var parentNode = await _feishuApiClient.GetWikiNodeInfoAsync(feishuAccessToken, new GetWikiNodeInfoRequest
            {
                Token = wikiWebConfig.ParentNodeToken,
            });

            workNodes.Enqueue(parentNode.Data.Node);
        }

        // 爬取每一个链接
        while (workNodes.Count > 0)
        {
            _databaseContext.ChangeTracker.Clear();

            // 每次爬取之前检查状态
            (isBreak, _) = await SetStateAsync(message.ConfigId, WorkerState.Processing, "正在爬取页面");
            if (isBreak)
            {
                return;
            }

            var currentNode = workNodes.Dequeue();

            try
            {
                // 检查这个地址有没有对应的文档
                var overridePage = await CheckExistDocumentAsync(wikiWebConfigEntity.Id, currentNode.NodeToken, !wikiWebConfig.IsOverExistPage);

                if (!overridePage.IsNext)
                {
                    await SetPageAsync(message.WikiId, message.ConfigId, currentNode, WorkerState.Cancal);
                    continue;
                }

                // 先抓取页面，保存的是对应的文件，但是不涉及业务
                var uploadResult = await CrawlerPageAsync(workNodes, wikiWebConfig, feishuAccessToken, currentNode, message.WikiId, message.ConfigId);

                // 如果页面没有任何变化，说明网页完全一样，跳过后续处理
                if (overridePage.WikiDocument != null && overridePage.WikiDocument.FileId == uploadResult.FileId)
                {
                    await SetPageAsync(message.WikiId, message.ConfigId, currentNode, WorkerState.Cancal);
                    continue;
                }

                // 新的页面，之前完全没有记录，直接插入
                if (overridePage.WikiConfigDocument == null)
                {
                    // 片段这个文件是否被其他配置使用，或者被当前配置的其他文档使用了
                    var existingDocument = await _databaseContext.WikiDocuments.AsNoTracking()
                        .Where(x => x.FileId == uploadResult.FileId)
                        .FirstOrDefaultAsync();

                    // 主要被其它地方使用了，都忽略本次操作
                    if (existingDocument != null)
                    {
                        await SetPageAsync(message.WikiId, message.ConfigId, currentNode, WorkerState.Cancal);
                        continue;
                    }

                    var documentEntity = new WikiDocumentEntity
                    {
                        WikiId = wikiWebConfigEntity.WikiId,
                        FileId = uploadResult.FileId,
                        FileName = BuildMarkdownFileName(currentNode.Title, uploadResult.FileMd5) + ".md",
                        ObjectKey = uploadResult.ObjectKey,
                        FileType = ".md",
                        IsEmbedding = false,
                    };

                    await _databaseContext.WikiDocuments.AddAsync(documentEntity);
                    await _databaseContext.SaveChangesAsync();

                    var pageEntity = new WikiPluginConfigDocumentEntity
                    {
                        WikiId = wikiWebConfigEntity.WikiId,
                        ConfigId = wikiWebConfigEntity.Id,
                        RelevanceKey = currentNode.NodeToken,
                        RelevanceValue = currentNode.ObjToken,
                        CreateUserId = wikiWebConfigEntity.UpdateUserId,
                        UpdateUserId = wikiWebConfigEntity.UpdateUserId,
                        WikiDocumentId = documentEntity.Id
                    };

                    await _databaseContext.WikiPluginConfigDocuments.AddAsync(pageEntity);
                    await _databaseContext.SaveChangesAsync();

                    await SetPageAsync(message.WikiId, message.ConfigId, currentNode, WorkerState.Successful);
                    continue;
                }

                // 记录存在，重新爬取后，碰到页面有没有变化
                if (overridePage.WikiDocument != null)
                {
                    if (overridePage.WikiDocument.FileId == uploadResult.FileId)
                    {
                        await SetPageAsync(message.WikiId, configId: message.ConfigId, currentNode, WorkerState.Cancal);
                        continue;
                    }
                }

                // 存在，并且需要覆盖，因为抓取的页面有变化
                {
                    // 片段这个文件是否被其他配置使用，或者被当前配置的其他文档使用了
                    var existingDocument = await _databaseContext.WikiDocuments.AsNoTracking()
                        .Where(x => x.FileId == uploadResult.FileId)
                        .FirstOrDefaultAsync();

                    // 主要被其它地方使用了，都忽略本次操作
                    if (existingDocument != null)
                    {
                        await SetPageAsync(message.WikiId, configId: message.ConfigId, currentNode, WorkerState.Cancal);
                        continue;
                    }

                    // 先删除旧的文件
                    await _mediator.Send(new DeleteFileCommand
                    {
                        FileIds = new[] { overridePage.WikiDocument!.FileId }
                    });

                    var documentEntity = overridePage.WikiDocument!;
                    documentEntity.FileId = uploadResult.FileId;
                    documentEntity.FileName = BuildMarkdownFileName(currentNode.Title, uploadResult.FileMd5) + ".md";
                    documentEntity.ObjectKey = uploadResult.ObjectKey;
                    documentEntity.FileType = ".md";

                    _databaseContext.WikiDocuments.Update(documentEntity);
                    await _databaseContext.SaveChangesAsync();

                    var pageEntity = overridePage.WikiConfigDocument!;
                    pageEntity.WikiDocumentId = documentEntity.Id;
                    _databaseContext.WikiPluginConfigDocuments.Update(pageEntity);
                    await _databaseContext.SaveChangesAsync();

                    await SetPageAsync(message.WikiId, message.ConfigId, currentNode, WorkerState.Successful);
                }

                continue;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Crawler url error:{NodeToken}", currentNode.NodeToken);
                await SetPageAsync(message.WikiId, message.ConfigId, currentNode, WorkerState.Failed, ex);
            }
        }

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
                    IsEmbedSourceText = message.Command.AutoProcessConfig.IsEmbedSourceText,
                    Partion = message.Command.AutoProcessConfig.Partion,
                    PreprocessStrategyType = message.Command.AutoProcessConfig.PreprocessStrategyType,
                    ThreadCount = message.Command.AutoProcessConfig.ThreadCount,
                    DocumentIds = documentIds,
                    PreprocessStrategyAiModel = message.Command.AutoProcessConfig.PreprocessStrategyAiModel,
                    IsEmbedding = message.Command.AutoProcessConfig.IsEmbedding
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Can't start WikiBatchProcessDocumentCommand");
        }
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWikiFeishuMessage message)
    {
        await SetExceptionStateAsync(message!.ConfigId, WorkerState.Failed, ex);
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWikiFeishuMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }

    private async Task<FileUploadResult> CrawlerPageAsync(Queue<WikiNode> workNodeTokens, WikiFeishuConfig wikiWebConfig, string feishuAccessToken, WikiNode currentNode, int wikiId, int configId)
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

        // 识别当前文档的子文档
        await GetWikiNodes(wikiId, configId, feishuAccessToken, wikiWebConfig.SpaceId, currentNode.NodeToken, workNodeTokens);

        return await SaveContentAsync(wikiId, stringBuilder.ToString());
    }

    // 获取当前层次的所有子节点
    private async Task GetWikiNodes(int wikiId, int configId, string feishuAccessToken, string spaceId, string nodeToken, Queue<WikiNode> workNodes)
    {
        try
        {
            List<WikiNode> newWikiNodes = new();

            var currentNodes = await _feishuApiClient.GetWikiNodesAsync(feishuAccessToken, spaceId, new GetWikiNodesRequest
            {
                PageSize = 50,
                ParentNodeToken = nodeToken
            });

            _feishuApiClient.CheckCode(currentNodes);

            if (currentNodes.Data.Items == null || currentNodes.Data.Items.Count == 0)
            {
                return;
            }

            foreach (var item in currentNodes.Data.Items)
            {
                workNodes.Enqueue(item);
                newWikiNodes.Add(item);
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
                    workNodes.Enqueue(item);
                    newWikiNodes.Add(item);
                }
            }

            await SetPageAsync(wikiId, configId, newWikiNodes, WorkerState.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain the sub-node of the Feishu Knowledge Space. It might be due to insufficient permissions or incorrect configuration.");
        }
    }

    private async Task<FileUploadResult> SaveContentAsync(int wikiId, string text)
    {
        // 将文档存储为文件
        var filePath = Path.Combine(Path.GetTempPath(), DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".md");
        await File.WriteAllTextAsync(filePath, text);
        using var streamReader = File.OpenRead(filePath);

        // 计算文件 md5
        var md5 = HashHelper.ComputeFileMd5(filePath);
        streamReader.Seek(0, SeekOrigin.Begin);

        // 上传文件
        var uploadResult = await _mediator.Send(new UploadFileStreamCommand
        {
            MD5 = md5,
            ContentType = "text/markdown",
            FileSize = (int)new FileInfo(filePath).Length,
            ObjectKey = $"{wikiId}/{md5}.md",
            FileStream = streamReader
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

    private async Task<(bool IsNext, WikiPluginConfigDocumentEntity? WikiConfigDocument, WikiDocumentEntity? WikiDocument)> CheckExistDocumentAsync(int configId, string nodeToken, bool isIgnore)
    {
        // 查找
        var wikiConfigDocument = await _databaseContext.WikiPluginConfigDocuments
            .FirstOrDefaultAsync(x => x.ConfigId == configId && x.RelevanceKey == nodeToken);

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

    private async Task SetPageAsync(int wikiId, int configId, WikiNode wikiNode, WorkerState? workerState = null, Exception? ex = null)
    {
        var existState = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WikiId == wikiId && x.ConfigId == configId && x.RelevanceKey == wikiNode.ObjToken);

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
                RelevanceKey = wikiNode.NodeToken,
                RelevanceValue = wikiNode.ObjToken,
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

    private async Task SetPageAsync(int wikiId, int configId, List<WikiNode> wikiNodes, WorkerState? workerState = null, Exception? ex = null)
    {
        if (wikiNodes == null || wikiNodes.Count == 0)
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

        var targetNodes = wikiNodes
            .Distinct()
            .ToList();

        if (targetNodes.Count == 0)
        {
            return;
        }

        var tagerNodeTokens = targetNodes.Select(x => x.NodeToken).ToList();

        var existingNodeTokens = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
            .Where(x => x.WikiId == wikiId && x.ConfigId == configId && tagerNodeTokens.Contains(x.RelevanceKey))
            .Select(x => new WikiNode
            {
                NodeToken = x.RelevanceKey,
                ObjToken = x.RelevanceValue
            })
            .ToListAsync();

        var newNodeTokens = targetNodes.Except(existingNodeTokens).ToList();
        if (newNodeTokens.Count == 0)
        {
            return;
        }

        var entities = newNodeTokens.Select(item => new WikiPluginConfigDocumentStateEntity
        {
            WikiId = wikiId,
            Message = message,
            RelevanceKey = item.NodeToken,
            RelevanceValue = item.ObjToken,
            ConfigId = configId,
            State = (int)workerState,
        });

        await _databaseContext.WikiPluginConfigDocumentStates.AddRangeAsync(entities);
        await _databaseContext.SaveChangesAsync();
        _databaseContext.ChangeTracker.Clear();
    }

    private static string BuildMarkdownFileName(string? rawTitle, string md5)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return md5;
        }

        var trimmedTitle = rawTitle.Trim();
        var builder = new StringBuilder(trimmedTitle.Length);

        foreach (var ch in trimmedTitle)
        {
            if (char.IsWhiteSpace(ch))
            {
                builder.Append('_');
                continue;
            }

            if (char.IsControl(ch) || Array.IndexOf(InvalidFileNameChars, ch) >= 0)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(ch);
        }

        var sanitized = builder.ToString().Trim('_');

        if (string.IsNullOrEmpty(sanitized))
        {
            return md5;
        }

        if (sanitized.Length > MaxFileNameLength)
        {
            sanitized = sanitized[..MaxFileNameLength];
        }

        return sanitized;
    }
}