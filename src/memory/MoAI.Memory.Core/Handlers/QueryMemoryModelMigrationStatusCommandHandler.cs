using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Infra.Models;
using MoAI.Memory.Models;
using MoAI.Memory.Queries;
using MoAI.Memory.Queries.Responses;
using MoAI.Memory.Services;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMemoryModelMigrationStatusCommand"/>
/// 返回当前生效模型/维度、平台记忆总数与最近一次迁移任务（活跃任务优先，含进度），供设置页判定锁定与轮询迁移进度.
/// </summary>
public class QueryMemoryModelMigrationStatusCommandHandler : IRequestHandler<QueryMemoryModelMigrationStatusCommand, QueryMemoryModelMigrationStatusResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMemoryModelMigrationStatusCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryMemoryModelMigrationStatusCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryMemoryModelMigrationStatusResponse> Handle(QueryMemoryModelMigrationStatusCommand request, CancellationToken cancellationToken)
    {
        var currentModelValue = await ResolveSettingValueAsync(SettingDefinitions.MemoryEmbeddingModelKey, cancellationToken);
        var currentDimensionsValue = await ResolveSettingValueAsync(SettingDefinitions.MemoryEmbeddingDimensionsKey, cancellationToken);
        var currentModelId = Guid.TryParse(currentModelValue, out var parsedModelId) ? parsedModelId : Guid.Empty;
        var currentDimensions = int.TryParse(currentDimensionsValue, out var parsedDimensions) ? parsedDimensions : 0;

        var totalMemoryCount = await _databaseContext.AppMemoryShards.AsNoTracking()
            .SumAsync(x => (int?)x.MemoryCount, cancellationToken) ?? 0;

        var modelNames = await ResolveModelNamesAsync(new[] { currentModelId }.Concat(await SelectTaskTargetModelIdsAsync(cancellationToken)), cancellationToken);

        var latestTask = await QueryLatestTaskAsync(cancellationToken);
        var activeMigration = latestTask == null ? null : ToTaskState(latestTask, modelNames);

        return new QueryMemoryModelMigrationStatusResponse
        {
            CurrentModelId = currentModelId,
            CurrentModelName = currentModelId == Guid.Empty ? string.Empty : modelNames.GetValueOrDefault(currentModelId, string.Empty),
            CurrentDimensions = currentDimensions,
            TotalMemoryCount = totalMemoryCount,
            ActiveMigration = activeMigration,
        };
    }

    /// <summary>
    /// 查询最近一次迁移任务：活跃任务（Wait/Processing）优先，否则取最近更新的一条.
    /// </summary>
    private async Task<WorkerTaskProjection?> QueryLatestTaskAsync(CancellationToken cancellationToken)
    {
        var rows = await _databaseContext.WorkerTasks.AsNoTracking()
            .Where(x => x.BindType == MemoryMigrationTask.BindType && x.IsDeleted == 0)
            .OrderByDescending(x => x.CreateTime)
            .Take(10)
            .Select(x => new WorkerTaskProjection(x.Id, x.State, x.Message, x.Data, x.CreateTime, x.UpdateTime))
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        return rows.FirstOrDefault(x => x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing)
            ?? rows[0];
    }

    /// <summary>
    /// 收集候选模型 id（当前模型 + 近期迁移任务目标模型）用于批量取名称.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> SelectTaskTargetModelIdsAsync(CancellationToken cancellationToken)
    {
        var rows = await _databaseContext.WorkerTasks.AsNoTracking()
            .Where(x => x.BindType == MemoryMigrationTask.BindType && x.IsDeleted == 0)
            .OrderByDescending(x => x.CreateTime)
            .Take(10)
            .Select(x => x.Data)
            .ToListAsync(cancellationToken);

        return rows
            .Select(data =>
            {
                try
                {
                    return JsonSerializer.Deserialize<MemoryModelMigrationTaskData>(data, MemoryModelMigrationTaskData.JsonOptions)?.TargetModelId ?? Guid.Empty;
                }
                catch (JsonException)
                {
                    return Guid.Empty;
                }
            })
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static MemoryMigrationTaskState ToTaskState(WorkerTaskProjection task, IReadOnlyDictionary<Guid, string> modelNames)
    {
        MemoryModelMigrationTaskData? data = null;
        try
        {
            data = JsonSerializer.Deserialize<MemoryModelMigrationTaskData>(task.Data, MemoryModelMigrationTaskData.JsonOptions);
        }
        catch (JsonException)
        {
            // 任务数据损坏时按空数据处理，保留状态与消息
        }

        return new MemoryMigrationTaskState
        {
            TaskId = task.Id,
            State = task.State,
            Message = task.Message,
            TargetModelId = data?.TargetModelId ?? Guid.Empty,
            TargetModelName = data != null ? modelNames.GetValueOrDefault(data.TargetModelId, string.Empty) : string.Empty,
            TargetDimensions = data?.TargetDimensions ?? 0,
            TotalCount = data?.TotalCount ?? 0,
            MigratedCount = data?.MigratedCount ?? 0,
            CreateTime = task.CreateTime,
            UpdateTime = task.UpdateTime,
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveModelNamesAsync(IEnumerable<Guid> modelIds, CancellationToken cancellationToken)
    {
        var ids = modelIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await _databaseContext.AiModels.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.Id, x => x.Name);
    }

    /// <summary>
    /// 设置项当前生效值（库值优先，缺失回退注册表默认值）.
    /// </summary>
    private async Task<string> ResolveSettingValueAsync(string key, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.Settings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        return entity != null && !string.IsNullOrWhiteSpace(entity.Value)
            ? entity.Value
            : SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;
    }

    /// <summary>
    /// 迁移任务行投影.
    /// </summary>
    private sealed record WorkerTaskProjection(Guid Id, int State, string Message, string Data, DateTimeOffset CreateTime, DateTimeOffset UpdateTime);
}
