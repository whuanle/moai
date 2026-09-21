using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Feishu.Models;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 外部源同步服务：拉取外部文档（当前支持飞书知识空间文档）并与上次同步的内容哈希比对，
/// 仅对新增或内容变化的文档更新知识库文档并触发工作流；支持单文档刷新（供云文档变更事件调用）.
/// </summary>
[InjectOnScoped]
public class WikiSourceSyncService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SyncLocks = new();

    private readonly DatabaseContext _databaseContext;
    private readonly IFeishuDriveClient _feishuDriveClient;
    private readonly WikiSourceContentWriter _contentWriter;
    private readonly WikiSourceCrawlerService _crawlerService;
    private readonly WikiSourceWorkflowRunner _workflowRunner;
    private readonly ILogger<WikiSourceSyncService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSourceSyncService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="feishuDriveClient">飞书云文档客户端.</param>
    /// <param name="contentWriter">外部源内容落库器.</param>
    /// <param name="crawlerService">网页爬虫同步器.</param>
    /// <param name="workflowRunner">外部源工作流执行器.</param>
    /// <param name="logger">日志.</param>
    public WikiSourceSyncService(
        DatabaseContext databaseContext,
        IFeishuDriveClient feishuDriveClient,
        WikiSourceContentWriter contentWriter,
        WikiSourceCrawlerService crawlerService,
        WikiSourceWorkflowRunner workflowRunner,
        ILogger<WikiSourceSyncService> logger)
    {
        _databaseContext = databaseContext;
        _feishuDriveClient = feishuDriveClient;
        _contentWriter = contentWriter;
        _crawlerService = crawlerService;
        _workflowRunner = workflowRunner;
        _logger = logger;
    }

    /// <summary>
    /// 同步整个外部源：拉取全部文档并增量更新.
    /// </summary>
    /// <param name="source">外部源实体.</param>
    /// <param name="force">是否忽略哈希比对强制处理全部文档.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>同步结果.</returns>
    public async Task<SyncWikiSourceCommandResponse> SyncAsync(WikiSourceEntity source, bool force, CancellationToken cancellationToken = default)
    {
        var semaphore = SyncLocks.GetOrAdd(source.Id, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            return new SyncWikiSourceCommandResponse { Message = "该外部源正在同步中，请稍后再试." };
        }

        try
        {
            return await SyncCoreAsync(source, force, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// 刷新单个外部文档（云文档变更事件触发）：按文档 token 定位映射并重新拉取比对.
    /// </summary>
    /// <param name="sourceId">外部源 id.</param>
    /// <param name="docToken">外部文档内容标识（飞书为 obj_token）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否命中并处理成功.</returns>
    public async Task<bool> RefreshDocumentAsync(Guid sourceId, string docToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docToken))
        {
            return false;
        }

        var mapping = await _databaseContext.WikiSourceDocuments
            .FirstOrDefaultAsync(x => x.SourceId == sourceId && x.ExternalDocToken == docToken, cancellationToken);

        if (mapping == null)
        {
            return false;
        }

        var source = await _databaseContext.WikiSources.FirstOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
        if (source == null || !source.IsEnable)
        {
            return false;
        }

        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == source.WikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            return false;
        }

        var config = WikiSourceConfigJson.DeserializeFeishu(source.Config);
        if (config == null)
        {
            return false;
        }

        try
        {
            var content = await _feishuDriveClient.GetDocxRawContentAsync(config.FeishuAppId, docToken, cancellationToken);
            var hash = WikiSourceContentWriter.ComputeSha256(content);

            if (string.Equals(mapping.ContentHash, hash, StringComparison.Ordinal))
            {
                mapping.LastSyncTime = DateTimeOffset.UtcNow;
                mapping.LastError = string.Empty;
                await _databaseContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            var node = new FeishuWikiNode
            {
                NodeToken = mapping.ExternalKey,
                ObjToken = mapping.ExternalDocToken,
                ObjType = "docx",
                Title = mapping.ExternalTitle,
                SpaceId = config.SpaceId ?? string.Empty,
                ObjEditTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            };

            await _contentWriter.WriteAsync(
                wiki,
                source,
                new WikiSourceContentItem
                {
                    ExternalKey = node.NodeToken,
                    ExternalDocToken = node.ObjToken,
                    Title = node.Title,
                    Path = mapping.ExternalPath,
                    Content = content,
                    ContentHash = hash,
                    Revision = node.ObjEditTime ?? string.Empty,
                },
                mapping,
                cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "外部源文档刷新失败. SourceId={SourceId} DocToken={DocToken}", sourceId, docToken);
            mapping.Status = (int)WikiSourceDocumentStatus.Failed;
            mapping.LastError = Truncate(ex.Message, 1000);
            await _databaseContext.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    /// <summary>
    /// 对外部源下的文档发起云文档事件订阅（开启事件订阅时调用），失败仅记录日志.
    /// </summary>
    /// <param name="source">外部源实体.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task SubscribeDocumentsAsync(WikiSourceEntity source, CancellationToken cancellationToken = default)
    {
        var config = WikiSourceConfigJson.DeserializeFeishu(source.Config);
        if (config == null)
        {
            return;
        }

        var tokens = await _databaseContext.WikiSourceDocuments
            .Where(x => x.SourceId == source.Id && x.ExternalDocToken != string.Empty)
            .Select(x => x.ExternalDocToken)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens.Distinct())
        {
            try
            {
                await _feishuDriveClient.SubscribeDocEventAsync(config.FeishuAppId, token, "docx", cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "订阅飞书文档事件失败. SourceId={SourceId} DocToken={DocToken}", source.Id, token);
            }
        }
    }

    /// <summary>
    /// 取消外部源下文档的云文档事件订阅（关闭事件订阅或删除外部源时调用），失败仅记录日志.
    /// </summary>
    /// <param name="source">外部源实体.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task UnsubscribeDocumentsAsync(WikiSourceEntity source, CancellationToken cancellationToken = default)
    {
        var config = WikiSourceConfigJson.DeserializeFeishu(source.Config);
        if (config == null)
        {
            return;
        }

        var tokens = await _databaseContext.WikiSourceDocuments
            .Where(x => x.SourceId == source.Id && x.ExternalDocToken != string.Empty)
            .Select(x => x.ExternalDocToken)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens.Distinct())
        {
            try
            {
                await _feishuDriveClient.UnsubscribeDocEventAsync(config.FeishuAppId, token, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "取消订阅飞书文档事件失败. SourceId={SourceId} DocToken={DocToken}", source.Id, token);
            }
        }
    }

    private async Task<SyncWikiSourceCommandResponse> SyncCoreAsync(WikiSourceEntity source, bool force, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == source.WikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            return Fail(source, "知识库不存在.");
        }

        // 按外部源类型分发：爬虫走独立的抓取-限流管线，飞书文档走云文档 API
        return source.SourceType switch
        {
            (int)WikiSourceType.Crawler => await SyncCrawlerAsync(source, force, cancellationToken),
            (int)WikiSourceType.FeishuDoc => await SyncFeishuAsync(source, wiki, force, cancellationToken),
            _ => Fail(source, "该外部源类型暂不支持同步."),
        };
    }

    /// <summary>
    /// 爬虫类型外部源的同步：委托给 <see cref="WikiSourceCrawlerService"/> 完成抓取、限流与增量比对.
    /// </summary>
    private async Task<SyncWikiSourceCommandResponse> SyncCrawlerAsync(WikiSourceEntity source, bool force, CancellationToken cancellationToken)
    {
        CrawlerSyncResult result;
        try
        {
            result = await _crawlerService.CrawlAsync(source, force, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "外部源爬取失败. SourceId={SourceId}", source.Id);
            return Fail(source, ex is BusinessException business ? business.Message : $"爬取失败：{ex.Message}");
        }

        var summary = result.BuildSummary();
        source.LastSyncTime = DateTimeOffset.UtcNow;
        source.LastSyncStatus = result.Failed > 0 ? (int)WikiSourceSyncStatus.Failed : (int)WikiSourceSyncStatus.Success;
        source.LastSyncMessage = Truncate(summary, 1000);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SyncWikiSourceCommandResponse
        {
            Total = result.Total,
            Created = result.Created,
            Updated = result.Updated,
            Unchanged = result.Unchanged,
            Skipped = result.Skipped,
            Failed = result.Failed,
            WorkflowTriggered = result.WorkflowTriggered,
            Message = summary,
            Items = result.Items.Select(x => new SyncWikiSourceDocumentResult
            {
                ExternalKey = x.Url,
                Title = x.Title,
                DocumentId = x.DocumentId,
                Result = x.Result,
                Message = x.Message,
            }).ToList(),
        };
    }

    private async Task<SyncWikiSourceCommandResponse> SyncFeishuAsync(WikiSourceEntity source, WikiEntity wiki, bool force, CancellationToken cancellationToken)
    {
        var items = new List<SyncWikiSourceDocumentResult>();

        var config = WikiSourceConfigJson.DeserializeFeishu(source.Config);
        if (config == null || config.FeishuAppId == Guid.Empty || string.IsNullOrWhiteSpace(config.NodeToken))
        {
            return Fail(source, "外部源配置不完整，请检查飞书应用与节点 token.");
        }

        List<(FeishuWikiNode Node, string Path)> nodes;
        try
        {
            nodes = await CollectNodesAsync(config, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "外部源拉取文档列表失败. SourceId={SourceId}", source.Id);
            return Fail(source, ex is BusinessException business ? business.Message : $"拉取文档列表失败：{ex.Message}");
        }

        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var skipped = 0;
        var failed = 0;
        var triggered = 0;

        foreach (var (node, path) in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(node.ObjType, "docx", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                items.Add(new SyncWikiSourceDocumentResult
                {
                    ExternalKey = node.NodeToken,
                    Title = node.Title,
                    Result = "skipped",
                    Message = $"暂不支持的文档类型：{node.ObjType}",
                });
                continue;
            }

            try
            {
                var content = await _feishuDriveClient.GetDocxRawContentAsync(config.FeishuAppId, node.ObjToken, cancellationToken);
                var hash = WikiSourceContentWriter.ComputeSha256(content);

                var mapping = await _databaseContext.WikiSourceDocuments
                    .FirstOrDefaultAsync(x => x.SourceId == source.Id && x.ExternalKey == node.NodeToken, cancellationToken);

                if (mapping != null && !force && string.Equals(mapping.ContentHash, hash, StringComparison.Ordinal))
                {
                    unchanged++;
                    mapping.LastSyncTime = DateTimeOffset.UtcNow;
                    mapping.LastError = string.Empty;
                    await _databaseContext.SaveChangesAsync(cancellationToken);
                    items.Add(new SyncWikiSourceDocumentResult
                    {
                        ExternalKey = node.NodeToken,
                        Title = node.Title,
                        DocumentId = mapping.DocumentId,
                        Result = "unchanged",
                        Message = "内容无变化",
                    });
                    continue;
                }

                var isNew = mapping == null;
                var write = await _contentWriter.WriteAsync(
                    wiki,
                    source,
                    new WikiSourceContentItem
                    {
                        ExternalKey = node.NodeToken,
                        ExternalDocToken = node.ObjToken,
                        Title = node.Title,
                        Path = path,
                        Content = content,
                        ContentHash = hash,
                        Revision = node.ObjEditTime ?? string.Empty,
                    },
                    mapping,
                    cancellationToken);

                var documentId = write.DocumentId;
                var taskId = write.TaskId;
                var message = write.Message;

                if (isNew)
                {
                    created++;
                }
                else
                {
                    updated++;
                }

                if (taskId.HasValue)
                {
                    triggered++;
                }

                items.Add(new SyncWikiSourceDocumentResult
                {
                    ExternalKey = node.NodeToken,
                    Title = node.Title,
                    DocumentId = documentId,
                    TaskId = taskId,
                    Result = isNew ? "created" : "updated",
                    Message = message,
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                _logger.LogWarning(ex, "外部源文档同步失败. SourceId={SourceId} NodeToken={NodeToken}", source.Id, node.NodeToken);
                items.Add(new SyncWikiSourceDocumentResult
                {
                    ExternalKey = node.NodeToken,
                    Title = node.Title,
                    Result = "failed",
                    Message = Truncate(ex.Message, 500),
                });
            }
        }

        var summary = $"共 {nodes.Count} 个文档：新建 {created}，更新 {updated}，无变化 {unchanged}，跳过 {skipped}，失败 {failed}" + (triggered > 0 ? $"，触发工作流 {triggered}" : string.Empty);
        source.LastSyncTime = DateTimeOffset.UtcNow;
        source.LastSyncStatus = failed > 0 ? (int)WikiSourceSyncStatus.Failed : (int)WikiSourceSyncStatus.Success;
        source.LastSyncMessage = Truncate(summary, 1000);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (source.IsEventSubscription)
        {
            await SubscribeDocumentsAsync(source, cancellationToken);
        }

        return new SyncWikiSourceCommandResponse
        {
            Total = nodes.Count,
            Created = created,
            Updated = updated,
            Unchanged = unchanged,
            Skipped = skipped,
            Failed = failed,
            WorkflowTriggered = triggered,
            Message = summary,
            Items = items,
        };
    }

    /// <summary>
    /// 收集飞书知识空间下的节点列表（按深度/数量上限遍历）.
    /// </summary>
    /// <remarks>
    /// 单文档落库逻辑统一由 <see cref="WikiSourceContentWriter"/> 承载，飞书同步与网页爬虫共用，
    /// 见 <see cref="WikiSourceContentWriter.WriteAsync"/>.
    /// </remarks>
    /// <param name="config">飞书文档源配置.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>节点与路径列表.</returns>
    private async Task<List<(FeishuWikiNode Node, string Path)>> CollectNodesAsync(WikiSourceFeishuConfig config, CancellationToken cancellationToken)
    {
        var root = await _feishuDriveClient.GetWikiNodeAsync(config.FeishuAppId, config.NodeToken, cancellationToken);
        if (root == null)
        {
            throw new BusinessException("无法访问该飞书节点，请检查节点 token 与应用是否已被添加为文档应用.") { StatusCode = 400 };
        }

        var result = new List<(FeishuWikiNode Node, string Path)> { (root, root.Title) };
        if (!config.IncludeSubNodes)
        {
            return result;
        }

        var limit = config.MaxDocuments > 0
            ? Math.Min(config.MaxDocuments, WikiSourceDefaults.MaxDocumentsLimit)
            : WikiSourceDefaults.DefaultMaxDocuments;
        var maxDepth = config.MaxDepth > 0 ? Math.Min(config.MaxDepth, WikiSourceDefaults.MaxDepthLimit) : WikiSourceDefaults.MaxDepthLimit;
        var spaceId = string.IsNullOrWhiteSpace(root.SpaceId) ? config.SpaceId : root.SpaceId;

        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return result;
        }

        var queue = new Queue<(string NodeToken, string Path, int Depth)>();
        queue.Enqueue((root.NodeToken, root.Title, 1));

        while (queue.Count > 0 && result.Count < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (nodeToken, path, depth) = queue.Dequeue();

            if (depth > maxDepth)
            {
                continue;
            }

            var children = await _feishuDriveClient.ListChildNodesAsync(config.FeishuAppId, spaceId, nodeToken, cancellationToken);
            foreach (var child in children)
            {
                if (result.Count >= limit)
                {
                    break;
                }

                var childPath = string.IsNullOrWhiteSpace(path) ? child.Title : $"{path}/{child.Title}";
                result.Add((child, childPath));

                if (child.HasChild)
                {
                    queue.Enqueue((child.NodeToken, childPath, depth + 1));
                }
            }
        }

        return result;
    }

    private SyncWikiSourceCommandResponse Fail(WikiSourceEntity source, string message)
    {
        source.LastSyncTime = DateTimeOffset.UtcNow;
        source.LastSyncStatus = (int)WikiSourceSyncStatus.Failed;
        source.LastSyncMessage = Truncate(message, 1000);
        _databaseContext.SaveChanges();

        return new SyncWikiSourceCommandResponse { Message = message };
    }

    private static string Truncate(string value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
    }
}
