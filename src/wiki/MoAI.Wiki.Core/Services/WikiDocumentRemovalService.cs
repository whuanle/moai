using System;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Storage.Services;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库文档删除领域服务：向量、切片、元数据、提取正文、外部源映射、OSS 文件与内嵌图片的全链路清理。
/// 站内删除、外部接口删除与删除知识库级联共用，保证向量与业务表同步清理，不残留可被召回的孤儿向量.
/// </summary>
[InjectOnScoped]
public class WikiDocumentRemovalService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IWikiEmbeddingVectorStore _vectorStore;
    private readonly IStorageService _storageService;
    private readonly WikiDocumentProcessingService _processingService;
    private readonly ILogger<WikiDocumentRemovalService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiDocumentRemovalService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="processingService">文档内容处理服务.</param>
    /// <param name="logger">日志.</param>
    public WikiDocumentRemovalService(
        DatabaseContext databaseContext,
        IWikiEmbeddingVectorStore vectorStore,
        IStorageService storageService,
        WikiDocumentProcessingService processingService,
        ILogger<WikiDocumentRemovalService> logger)
    {
        _databaseContext = databaseContext;
        _vectorStore = vectorStore;
        _storageService = storageService;
        _processingService = processingService;
        _logger = logger;
    }

    /// <summary>
    /// 删除知识库文档及其全部关联数据：先删向量（依赖 wiki 配置解析维度）→ 软删文档/切片/元数据 →
    /// 清理提取正文与外部源映射（外部源同步会在下次拉取时按需重建文档）→ 删除 OSS 文件与内嵌图片.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documents">待删除文档（须属于该知识库）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task RemoveDocumentsAsync(int wikiId, IReadOnlyCollection<WikiDocumentEntity> documents, CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
        {
            return;
        }

        var documentIds = documents.Select(x => x.Id).ToList();

        // 向量清理放在软删之前：维度解析依赖 wiki 行与文档归属，集合缺失时静默跳过
        foreach (var document in documents)
        {
            try
            {
                await _vectorStore.DeleteDocumentVectorsAsync(wikiId, document.Id, cancellationToken);
            }
#pragma warning disable CA1031 // 单文档向量清理失败不应阻断删除主流程
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除文档向量失败（已跳过，召回侧有软删文档过滤兜底）. WikiId={WikiId}, DocumentId={DocumentId}", wikiId, document.Id);
            }
#pragma warning restore CA1031
        }

        _databaseContext.WikiDocuments.RemoveRange(documents);

        var chunks = await _databaseContext.WikiDocumentChunkContents
            .Where(x => documentIds.Contains(x.DocumentId))
            .ToArrayAsync(cancellationToken);
        if (chunks.Length > 0)
        {
            _databaseContext.WikiDocumentChunkContents.RemoveRange(chunks);
        }

        var metadata = await _databaseContext.WikiDocumentChunkMetadata
            .Where(x => documentIds.Contains(x.DocumentId))
            .ToArrayAsync(cancellationToken);
        if (metadata.Length > 0)
        {
            _databaseContext.WikiDocumentChunkMetadata.RemoveRange(metadata);
        }

        // 提取正文与外部源映射：历史实现漏删导致行残留，此处一并清理
        await _databaseContext.WikiDocumentContents
            .Where(x => documentIds.Contains(x.DocumentId))
            .ExecuteDeleteAsync(cancellationToken);
        await _databaseContext.WikiSourceDocuments
            .Where(x => documentIds.Contains(x.DocumentId))
            .ExecuteDeleteAsync(cancellationToken);

        // 删除 oss 文件
        var fileIds = documents.Select(x => (long)x.FileId).ToArray();
        await _storageService.DeleteFilesAsync(fileIds, cancellationToken);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 删除关联的内嵌图片附件登记，并清理不再被引用的图片文件
        await _processingService.DeleteDocumentImagesAsync(wikiId, documentIds, cancellationToken);

        // 推进索引纪元：文档集合变化使该库的检索经验聚类失效（复用时自愈回落完整检索）
        await BumpIndexEpochAsync(wikiId, cancellationToken);
    }

    /// <summary>
    /// 推进知识库索引纪元（检索经验聚类的新鲜度依据）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task BumpIndexEpochAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        await _databaseContext.Wikis
            .Where(x => x.Id == wikiId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IndexEpoch, x => x.IndexEpoch + 1)
                .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow), cancellationToken);
    }
}
