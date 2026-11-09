using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiModelMigrationStatusCommand"/>
/// 返回当前生效模型/维度、该库向量总数与最近一次迁移任务（活跃任务优先，含进度），供知识库详情页判定与轮询迁移进度.
/// </summary>
public class QueryWikiModelMigrationStatusCommandHandler : IRequestHandler<QueryWikiModelMigrationStatusCommand, QueryWikiModelMigrationStatusResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IWikiEmbeddingVectorStore _vectorStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiModelMigrationStatusCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    public QueryWikiModelMigrationStatusCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IWikiEmbeddingVectorStore vectorStore)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _vectorStore = vectorStore;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiModelMigrationStatusResponse> Handle(QueryWikiModelMigrationStatusCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var totalVectorCount = await _vectorStore.CountCollectionAsync(wiki.Id, cancellationToken);

        var latestTask = await QueryLatestTaskAsync(wiki.Id, cancellationToken);
        var taskData = latestTask == null ? null : TryDeserialize(latestTask.Data);

        // 当前模型 + 近期任务目标模型，一次批量取名称
        var modelIds = new List<Guid> { wiki.EmbeddingModelId };
        if (taskData != null)
        {
            modelIds.Add(taskData.TargetModelId);
        }

        var modelNames = await ResolveModelNamesAsync(modelIds, cancellationToken);
        var activeMigration = latestTask == null ? null : ToTaskState(latestTask, taskData, modelNames);

        return new QueryWikiModelMigrationStatusResponse
        {
            WikiId = wiki.Id,
            CurrentModelId = wiki.EmbeddingModelId,
            CurrentModelName = wiki.EmbeddingModelId == Guid.Empty ? string.Empty : modelNames.GetValueOrDefault(wiki.EmbeddingModelId, string.Empty),
            CurrentDimensions = wiki.EmbeddingDimensions,
            TotalVectorCount = totalVectorCount,
            ActiveMigration = activeMigration,
        };
    }

    /// <summary>
    /// 查询最近一次迁移任务：活跃任务（Wait/Processing）优先，否则取最近创建的一条.
    /// </summary>
    private async Task<WorkerTaskEntity?> QueryLatestTaskAsync(int wikiId, CancellationToken cancellationToken)
    {
        var rows = await _databaseContext.WorkerTasks.AsNoTracking()
            .Where(x => x.BindType == WikiModelMigrationTask.BindType && x.BindId == wikiId && x.IsDeleted == 0)
            .OrderByDescending(x => x.CreateTime)
            .Take(10)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        return rows.FirstOrDefault(x => x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing)
            ?? rows[0];
    }

    private static WikiModelMigrationTaskData? TryDeserialize(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WikiModelMigrationTaskData>(data, WikiModelMigrationTaskData.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WikiModelMigrationTaskState ToTaskState(WorkerTaskEntity task, WikiModelMigrationTaskData? data, IReadOnlyDictionary<Guid, string> modelNames)
    {
        return new WikiModelMigrationTaskState
        {
            TaskId = task.Id,
            State = task.State,
            Message = task.Message ?? string.Empty,
            TargetModelId = data?.TargetModelId ?? Guid.Empty,
            TargetModelName = data == null ? string.Empty : modelNames.GetValueOrDefault(data.TargetModelId, string.Empty),
            TargetDimensions = data?.TargetDimensions ?? 0,
            TotalCount = data?.TotalCount ?? 0,
            MigratedCount = data?.MigratedCount ?? 0,
            CreateTime = task.CreateTime,
            UpdateTime = task.UpdateTime,
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveModelNamesAsync(IReadOnlyCollection<Guid> modelIds, CancellationToken cancellationToken)
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
}
