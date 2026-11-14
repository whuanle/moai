using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Memory.Commands;
using MoAI.Memory.Events;
using MoAI.Memory.Models;
using MoAI.Memory.Services;
using Npgsql;

namespace MoAI.Memory.Handlers;

/// <summary>
/// <inheritdoc cref="StartMemoryModelMigrationCommand"/>
/// 校验口径：已配置统一模型、平台确有记忆、目标为公开启用中的 embedding 模型、目标与当前不同；
/// worker_task 登记迁移快照（目标/源模型维度 + 总条数）后投递 MQ，异步重建全部记忆向量.
/// </summary>
public class StartMemoryModelMigrationCommandHandler : IRequestHandler<StartMemoryModelMigrationCommand, StartMemoryModelMigrationCommandResponse>
{
    private const string EmbeddingKind = "embedding";
    private const string ActiveTaskConstraintName = "ux_worker_task_bind_active";

    private readonly DatabaseContext _databaseContext;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartMemoryModelMigrationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    public StartMemoryModelMigrationCommandHandler(DatabaseContext databaseContext, IMessagePublisher messagePublisher)
    {
        _databaseContext = databaseContext;
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task<StartMemoryModelMigrationCommandResponse> Handle(StartMemoryModelMigrationCommand request, CancellationToken cancellationToken)
    {
        var currentModelValue = await ResolveSettingValueAsync(SettingDefinitions.MemoryEmbeddingModelKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(currentModelValue))
        {
            throw new BusinessException("尚未配置统一向量化模型，无需迁移，直接保存设置即可.") { StatusCode = 400 };
        }

        var totalMemoryCount = await _databaseContext.AppMemoryShards.AsNoTracking()
            .SumAsync(x => (int?)x.MemoryCount, cancellationToken) ?? 0;
        if (totalMemoryCount <= 0)
        {
            throw new BusinessException("平台暂无记忆，无需迁移，直接保存设置即可.") { StatusCode = 400 };
        }

        // 目标模型须为公开启用中的 embedding 模型（渠道启用），与可选模型下拉同口径
        var targetModel = await _databaseContext.AiModels.AsNoTracking()
            .Where(m => m.Id == request.ModelId && m.Enabled && m.IsPublic && m.ModelKind == EmbeddingKind)
            .Join(
                _databaseContext.AiChannels.Where(c => c.Enabled),
                m => m.ChannelId,
                c => c.Id,
                (m, c) => new { m.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (targetModel == null)
        {
            throw new BusinessException("目标向量化模型不可用（须为公开启用中的 embedding 模型）.") { StatusCode = 400 };
        }

        var currentModelId = Guid.Parse(currentModelValue);
        var currentDimensions = await ResolveCurrentDimensionsAsync(cancellationToken);
        if (currentModelId == request.ModelId && currentDimensions == request.Dimensions)
        {
            throw new BusinessException("与当前向量化模型/维度一致，无需迁移.") { StatusCode = 400 };
        }

        var hasActiveTask = await _databaseContext.WorkerTasks.AsNoTracking()
            .AnyAsync(
                x => x.BindType == MemoryMigrationTask.BindType
                    && x.IsDeleted == 0
                    && (x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing),
                cancellationToken);
        if (hasActiveTask)
        {
            throw new BusinessException("已有进行中的迁移任务，请稍后再试.") { StatusCode = 409 };
        }

        var taskData = new MemoryModelMigrationTaskData
        {
            TargetModelId = request.ModelId,
            TargetDimensions = request.Dimensions,
            SourceModelId = currentModelId,
            SourceDimensions = currentDimensions,
            TotalCount = totalMemoryCount,
            MigratedCount = 0,
        };

        var task = new WorkerTaskEntity
        {
            Id = Guid.CreateVersion7(),
            BindType = MemoryMigrationTask.BindType,
            BindId = 0,
            State = (int)WorkerState.Wait,
            Message = "迁移任务已创建",
            Data = JsonSerializer.Serialize(taskData, MemoryModelMigrationTaskData.JsonOptions),
        };

        await _databaseContext.WorkerTasks.AddAsync(task, cancellationToken);
        try
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // 并发双击等场景命中活跃任务唯一索引
            if (IsActiveTaskUniqueConstraintViolation(ex))
            {
                throw new BusinessException("已有进行中的迁移任务，请稍后再试.") { StatusCode = 409 };
            }

            throw;
        }

        try
        {
            await _messagePublisher.AutoPublishAsync(new MemoryModelMigrationMessage { TaskId = task.Id });
        }
        catch (Exception ex)
        {
            task.State = (int)WorkerState.Failed;
            task.Message = ex.Message;
            await _databaseContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new StartMemoryModelMigrationCommandResponse { TaskId = task.Id };
    }

    /// <summary>
    /// 读取设置项当前生效值（库值优先，缺失回退注册表默认值）.
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
    /// 解析当前生效维度（非法值回退默认 1024）.
    /// </summary>
    private async Task<int> ResolveCurrentDimensionsAsync(CancellationToken cancellationToken)
    {
        var value = await ResolveSettingValueAsync(SettingDefinitions.MemoryEmbeddingDimensionsKey, cancellationToken);
        return int.TryParse(value, out var dimensions) && dimensions >= 1 && dimensions <= 2000
            ? dimensions
            : 1024;
    }

    private static bool IsActiveTaskUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgresException
                && string.Equals(postgresException.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
                && string.Equals(postgresException.ConstraintName, ActiveTaskConstraintName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
