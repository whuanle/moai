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
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Seed;
using MoAI.Infra.Models;
using MoAI.Memory.Models;

namespace MoAI.Memory.Services;

/// <summary>
/// <inheritdoc cref="IMemoryModelMigrationService"/>
/// 迁移执行口径：原子认领（Wait→Processing）→ 枚举有记忆的集合（应用集合 + 全局集合）→
/// 逐集合分页读旧记录（原文在 Content，仅换 Embedding）→ 批量重嵌写入临时表 →
/// 全部成功后逐集合事务换表（DROP 旧 + RENAME 临时）→ 最后才切换平台设置（迁移期间运行时仍按旧口径读旧表）；
/// 任一步失败删除全部临时表并置任务失败，原数据完好可重新发起；迁移期间记忆写入由 MemoryService 统一阻断.
/// </summary>
[InjectOnScoped]
public sealed class MemoryModelMigrationService : IMemoryModelMigrationService
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

    private const string EmbeddingKind = "embedding";

    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryVectorStore _vectorStore;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly ILogger<MemoryModelMigrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryModelMigrationService"/> class.
    /// </summary>
    public MemoryModelMigrationService(
        DatabaseContext databaseContext,
        IMemoryVectorStore vectorStore,
        IEmbeddingGeneratorProvider embeddingGeneratorProvider,
        ILogger<MemoryModelMigrationService> logger)
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
            _logger.LogInformation("Skip memory model migration because claim failed. TaskId={TaskId}, AffectedRows={AffectedRows}", taskId, claimed);
            return;
        }

        var taskDataJson = await _databaseContext.WorkerTasks.AsNoTracking()
            .Where(x => x.Id == taskId && x.IsDeleted == 0)
            .Select(x => x.Data)
            .FirstOrDefaultAsync(cancellationToken);
        MemoryModelMigrationTaskData? data;
        try
        {
            data = taskDataJson == null
                ? null
                : JsonSerializer.Deserialize<MemoryModelMigrationTaskData>(taskDataJson, MemoryModelMigrationTaskData.JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid memory migration task data json. TaskId={TaskId}", taskId);
            data = null;
        }

        if (data == null || data.TargetModelId == Guid.Empty || data.TargetDimensions <= 0)
        {
            await MarkFailedAsync(taskId, "迁移任务数据无效。", CancellationToken.None);
            return;
        }

        var begunApps = new List<Guid>();
        try
        {
            // 有记忆的集合：应用集合 + 全局集合（全局哨兵 appId 一并枚举，与 ClearAllAsync 口径一致）
            var appIds = await _databaseContext.AppMemoryShards.AsNoTracking()
                .Where(x => x.MemoryCount > 0)
                .Select(x => x.AppId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var generator = await ResolveGeneratorAsync(data.TargetModelId, cancellationToken);

            var total = 0;
            foreach (var appId in appIds)
            {
                await _vectorStore.BeginMigrationCollectionAsync(appId, data.TargetDimensions, cancellationToken);
                begunApps.Add(appId);
                total += await MigrateCollectionAsync(taskId, appId, generator, data, cancellationToken);
            }

            // 逐集合换表：旧表删除、临时表顶替（此后新维度生效）
            foreach (var appId in begunApps)
            {
                await _vectorStore.CompleteMigrationCollectionAsync(appId, cancellationToken);
            }

            // 换表全部成功后才切换平台设置（迁移期间运行时仍按旧口径读旧表）
            await UpdatePlatformSettingsAsync(data, cancellationToken);

            data.MigratedCount = total;
            await UpdateTaskAsync(taskId, WorkerState.Successful, $"迁移完成，共重建 {total} 条记忆。", data, CancellationToken.None);
            _logger.LogInformation("Memory model migration completed. TaskId={TaskId}, Collections={Collections}, Total={Total}", taskId, begunApps.Count, total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory model migration failed. TaskId={TaskId}", taskId);

            // 回滚临时表，原集合原样保留，可重新发起迁移
            foreach (var appId in begunApps)
            {
                try
                {
                    await _vectorStore.AbortMigrationCollectionAsync(appId, CancellationToken.None);
                }
                catch (Exception abortEx)
                {
                    _logger.LogError(abortEx, "Failed to abort memory migration temp collection. TaskId={TaskId}, AppId={AppId}", taskId, appId);
                }
            }

            await MarkFailedAsync(taskId, $"迁移失败：{ex.Message}（已回滚，原记忆未受影响，可重新发起）", CancellationToken.None);
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
            _logger.LogError(ex, "Failed to mark memory migration task as failed. TaskId={TaskId}", taskId);
        }
    }

    /// <summary>
    /// 迁移单个集合：分页读旧记录 → 批量重嵌（仅换 Embedding，其余字段原样保留）→ 写入临时表 → 回写进度.
    /// </summary>
    private async Task<int> MigrateCollectionAsync(Guid taskId, Guid appId, IEmbeddingGenerator<string, Embedding<float>> generator, MemoryModelMigrationTaskData data, CancellationToken cancellationToken)
    {
        var migrated = 0;
        while (true)
        {
            // 迁移期间原集合只读（写入已被阻断），skip 分页不重不漏
            var page = await _vectorStore.ListAllPageAsync(appId, migrated, ReadPageSize, cancellationToken);
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
                    batch[i].Embedding = embeddings[i].Vector;
                }

                await _vectorStore.UpsertMigrationCollectionAsync(appId, batch, data.TargetDimensions, cancellationToken);
                migrated += batch.Count;
            }

            // 每页回写一次进度（任务行 Message + Data，前端轮询展示）
            data.MigratedCount = migrated;
            await UpdateTaskAsync(taskId, WorkerState.Processing, $"正在迁移 {migrated}/{data.TotalCount} 条记忆 …", data, cancellationToken);
        }

        return migrated;
    }

    /// <summary>
    /// 解析目标向量化模型与渠道（公开启用中的 embedding 模型，teamId=0 公共口径）并构建向量生成器.
    /// </summary>
    private async Task<IEmbeddingGenerator<string, Embedding<float>>> ResolveGeneratorAsync(Guid modelId, CancellationToken cancellationToken)
    {
        var rows = await _databaseContext.AiModels.AsNoTracking()
            .Where(x => x.Id == modelId && x.Enabled && x.IsPublic && x.ModelKind == EmbeddingKind)
            .Join(
                _databaseContext.AiChannels.Where(c => c.Enabled),
                m => m.ChannelId,
                c => c.Id,
                (m, c) => new { Model = m, Channel = c })
            .FirstOrDefaultAsync(cancellationToken);
        if (rows == null)
        {
            throw new InvalidOperationException("目标向量化模型不可用（须为公开启用中的 embedding 模型）.");
        }

        return await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(rows.Model, rows.Channel, cancellationToken);
    }

    /// <summary>
    /// 迁移成功后切换平台统一模型/维度设置：直接落库绕过锁定校验（迁移语义本身就是解锁换模型）.
    /// </summary>
    private async Task UpdatePlatformSettingsAsync(MemoryModelMigrationTaskData data, CancellationToken cancellationToken)
    {
        await UpsertSettingAsync(SettingDefinitions.MemoryEmbeddingModelKey, data.TargetModelId.ToString(), cancellationToken);
        await UpsertSettingAsync(SettingDefinitions.MemoryEmbeddingDimensionsKey, data.TargetDimensions.ToString(), cancellationToken);
    }

    private async Task UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.Settings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (entity == null)
        {
            var definition = SettingDefinitions.Find(key);
            entity = new SettingEntity
            {
                Key = key,
                Name = definition?.Name ?? key,
                Description = definition?.Description ?? string.Empty,
                Value = value,
            };
            await _databaseContext.Settings.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.Value = value;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateTaskAsync(Guid taskId, WorkerState state, string message, MemoryModelMigrationTaskData data, CancellationToken cancellationToken)
    {
        await _databaseContext.WorkerTasks
            .Where(x => x.Id == taskId && x.IsDeleted == 0 && x.State == (int)WorkerState.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.State, (int)state)
                .SetProperty(x => x.Message, message)
                .SetProperty(x => x.Data, JsonSerializer.Serialize(data, MemoryModelMigrationTaskData.JsonOptions))
                .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow), cancellationToken);
    }

    private static string Truncate(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message) ? "未知错误。" : message.Trim();
        return normalized.Length <= MaxErrorMessageLength ? normalized : normalized[..MaxErrorMessageLength] + "…";
    }
}
