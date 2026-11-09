using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// <inheritdoc cref="IWikiModelMigrationService"/>
/// 迁移执行口径（与记忆模型迁移同构）：原子认领（Wait→Processing）→ 读旧集合分页记录（原文在 Content，仅换 Embedding）→
/// 批量重嵌写入临时集合（__wiki_mig_{id}）→ 全部成功后事务换表（DROP 旧 + RENAME 临时）→ 最后才更新知识库模型/维度配置
/// （迁移期间运行时召回仍按旧口径读旧表）；任一步失败删除临时集合并置任务失败，原数据完好可重新发起；
/// 迁移期间该库的提取/切割/向量化写入由 <see cref="EnsureNotMigratingAsync"/> 统一阻断.
/// </summary>
[InjectOnScoped]
public sealed class WikiModelMigrationService : IWikiModelMigrationService
{
    /// <summary>
    /// 分页读取旧记录页大小.
    /// </summary>
    private const int ReadPageSize = 256;

    /// <summary>
    /// 批量重嵌条数.
    /// </summary>
    private const int EmbedBatchSize = 64;

    /// <summary>
    /// 任务失败消息长度上限.
    /// </summary>
    private const int MaxErrorMessageLength = 500;

    private readonly DatabaseContext _databaseContext;
    private readonly IWikiEmbeddingVectorStore _vectorStore;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly ILogger<WikiModelMigrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiModelMigrationService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="logger">日志.</param>
    public WikiModelMigrationService(
        DatabaseContext databaseContext,
        IWikiEmbeddingVectorStore vectorStore,
        IEmbeddingGeneratorProvider embeddingGeneratorProvider,
        ILogger<WikiModelMigrationService> logger)
    {
        _databaseContext = databaseContext;
        _vectorStore = vectorStore;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        // 原子认领：仅 Wait → Processing 成功者执行，防 MQ 重复投递并发执行
        var claimed = await _databaseContext.WorkerTasks
            .Where(x => x.Id == taskId && x.IsDeleted == 0 && x.State == (int)WorkerState.Wait)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, (int)WorkerState.Processing)
                .SetProperty(x => x.Message, "迁移任务处理中")
                .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow), cancellationToken);
        if (claimed != 1)
        {
            _logger.LogInformation("Skip wiki model migration because claim failed. TaskId={TaskId}, AffectedRows={AffectedRows}", taskId, claimed);
            return;
        }

        var taskRow = await _databaseContext.WorkerTasks.AsNoTracking()
            .Where(x => x.Id == taskId && x.IsDeleted == 0)
            .Select(x => new { x.BindId, x.Data })
            .FirstOrDefaultAsync(cancellationToken);
        if (taskRow == null)
        {
            await MarkFailedAsync(taskId, "迁移任务不存在。", CancellationToken.None);
            return;
        }

        var wikiId = taskRow.BindId;
        WikiModelMigrationTaskData? data;
        try
        {
            data = taskRow.Data == null
                ? null
                : JsonSerializer.Deserialize<WikiModelMigrationTaskData>(taskRow.Data, WikiModelMigrationTaskData.JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid wiki migration task data json. TaskId={TaskId}", taskId);
            data = null;
        }

        if (data == null || data.TargetModelId == Guid.Empty || data.TargetDimensions <= 0 || wikiId <= 0)
        {
            await MarkFailedAsync(taskId, "迁移任务数据无效。", CancellationToken.None);
            return;
        }

        var begun = false;
        try
        {
            var wiki = await _databaseContext.Wikis.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == wikiId && x.IsDeleted == 0, cancellationToken)
                ?? throw new InvalidOperationException($"知识库不存在: {wikiId}");

            var generator = await ResolveGeneratorAsync(data.TargetModelId, wiki.TeamId, cancellationToken);

            var total = await _vectorStore.CountCollectionAsync(wikiId, cancellationToken);
            data.TotalCount = total;

            await _vectorStore.BeginMigrationCollectionAsync(wikiId, data.TargetDimensions, cancellationToken);
            begun = true;

            var migrated = await MigrateCollectionAsync(taskId, wikiId, generator, data, cancellationToken);

            // 换表：旧表删除、临时表顶替（此后新维度生效，召回自动走新向量）
            await _vectorStore.CompleteMigrationCollectionAsync(wikiId, cancellationToken);
            begun = false;

            // 换表成功后才更新知识库模型/维度配置（IsLock 保持 true，迁移是唯一合法换模型通道）
            await _databaseContext.Wikis
                .Where(x => x.Id == wikiId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.EmbeddingModelId, data.TargetModelId)
                    .SetProperty(x => x.EmbeddingDimensions, data.TargetDimensions)
                    .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow), cancellationToken);

            data.MigratedCount = migrated;
            await UpdateTaskAsync(taskId, WorkerState.Successful, $"迁移完成，共重建 {migrated} 条向量。", data, CancellationToken.None);
            _logger.LogInformation("Wiki model migration completed. TaskId={TaskId}, WikiId={WikiId}, Total={Total}", taskId, wikiId, migrated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki model migration failed. TaskId={TaskId}, WikiId={WikiId}", taskId, wikiId);

            // 回滚临时集合，原集合原样保留，可重新发起迁移
            if (begun)
            {
                try
                {
                    await _vectorStore.AbortMigrationCollectionAsync(wikiId, CancellationToken.None);
                }
                catch (Exception abortEx)
                {
                    _logger.LogError(abortEx, "Failed to abort wiki migration temp collection. TaskId={TaskId}, WikiId={WikiId}", taskId, wikiId);
                }
            }

            await MarkFailedAsync(taskId, $"迁移失败：{ex.Message}（已回滚，原向量未受影响，可重新发起）", CancellationToken.None);
        }
    }

    /// <inheritdoc/>
    public async Task MarkFailedAsync(Guid taskId, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _databaseContext.WorkerTasks
                .Where(x => x.Id == taskId && x.IsDeleted == 0
                    && (x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.State, (int)WorkerState.Failed)
                    .SetProperty(x => x.Message, Truncate(message))
                    .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark wiki migration task as failed. TaskId={TaskId}", taskId);
        }
    }

    /// <inheritdoc/>
    public async Task EnsureNotMigratingAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        var hasActiveTask = await _databaseContext.WorkerTasks.AsNoTracking()
            .AnyAsync(
                x => x.BindType == WikiModelMigrationTask.BindType
                    && x.BindId == wikiId
                    && x.IsDeleted == 0
                    && (x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing),
                cancellationToken);
        if (hasActiveTask)
        {
            throw new BusinessException("知识库正在迁移向量化模型，请等待迁移完成后再操作.") { StatusCode = 409 };
        }
    }

    /// <summary>
    /// 迁移单个集合：分页读旧记录 → 批量重嵌（仅换 Embedding，其余字段原样保留）→ 写入临时集合 → 回写进度.
    /// </summary>
    private async Task<int> MigrateCollectionAsync(Guid taskId, int wikiId, IEmbeddingGenerator<string, Embedding<float>> generator, WikiModelMigrationTaskData data, CancellationToken cancellationToken)
    {
        var migrated = 0;
        while (true)
        {
            // 迁移期间原集合只读（写入已被阻断），skip 分页不重不漏
            var page = await _vectorStore.ListAllPageAsync(wikiId, migrated, ReadPageSize, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            for (var offset = 0; offset < page.Count; offset += EmbedBatchSize)
            {
                var batch = page.Skip(offset).Take(EmbedBatchSize).ToList();
                var embeddings = await generator.GenerateAsync(
                    batch.Select(x => x.Content),
                    new EmbeddingGenerationOptions { Dimensions = data.TargetDimensions },
                    cancellationToken);

                // 生成的向量与输入内容按顺序一一对应
                for (var i = 0; i < batch.Count; i++)
                {
                    if (i >= embeddings.Count || embeddings[i].Vector.IsEmpty)
                    {
                        throw new InvalidOperationException($"向量模型未返回有效向量：ChunkId={batch[i].ChunkId}, MetadataType={batch[i].MetadataType}。");
                    }

                    batch[i].Embedding = embeddings[i].Vector;
                }

                await _vectorStore.UpsertMigrationCollectionAsync(wikiId, batch, data.TargetDimensions, cancellationToken);
                migrated += batch.Count;
            }

            // 每页回写一次进度（任务行 Message + Data，前端轮询展示）
            data.MigratedCount = migrated;
            await UpdateTaskAsync(taskId, WorkerState.Processing, $"正在迁移 {migrated}/{data.TotalCount} 条向量 …", data, cancellationToken);
        }

        return migrated;
    }

    /// <summary>
    /// 解析目标向量化模型与渠道（团队可用口径：团队自有 → 公开 → 渠道授权）并构建向量生成器.
    /// </summary>
    private async Task<IEmbeddingGenerator<string, Embedding<float>>> ResolveGeneratorAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                    select new { m, c };

        var candidates = await query.ToListAsync(cancellationToken);
        var model = candidates.FirstOrDefault(x => x.c.TeamId == teamId) ?? candidates.FirstOrDefault(x => x.m.IsPublic);
        if (model == null)
        {
            var authorized = await _databaseContext.AiModelAuthorizations
                .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
            if (!authorized)
            {
                throw new InvalidOperationException("目标向量化模型不可用（未授权给该团队或未启用）.");
            }

            model = candidates.FirstOrDefault();
        }

        if (model == null || !string.Equals(model.m.ModelKind, "embedding", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("目标向量化模型不可用（须为启用中的 embedding 模型）.");
        }

        return await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(model.m, model.c, cancellationToken);
    }

    private async Task UpdateTaskAsync(Guid taskId, WorkerState state, string message, WikiModelMigrationTaskData data, CancellationToken cancellationToken)
    {
        await _databaseContext.WorkerTasks
            .Where(x => x.Id == taskId && x.IsDeleted == 0 && x.State == (int)WorkerState.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, (int)state)
                .SetProperty(x => x.Message, message)
                .SetProperty(x => x.Data, JsonSerializer.Serialize(data, WikiModelMigrationTaskData.JsonOptions))
                .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow), cancellationToken);
    }

    private static string Truncate(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message) ? "未知错误。" : message.Trim();
        return normalized.Length <= MaxErrorMessageLength ? normalized : normalized[..MaxErrorMessageLength] + "…";
    }
}
