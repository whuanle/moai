using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;
using Npgsql;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="StartWikiModelMigrationCommand"/>
/// 校验口径：知识库存在且为团队管理员、该库确有向量、目标为团队可用 embedding 模型、目标与当前不同、无活跃迁移任务；
/// worker_task 登记迁移快照（BindId=知识库 id）后投递 MQ，异步重建该库全部向量，完成后更新知识库配置.
/// </summary>
public class StartWikiModelMigrationCommandHandler : IRequestHandler<StartWikiModelMigrationCommand, StartWikiModelMigrationCommandResponse>
{
    private const string EmbeddingKind = "embedding";
    private const string ActiveTaskConstraintName = "ux_worker_task_bind_active";

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IWikiEmbeddingVectorStore _vectorStore;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiModelMigrationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    public StartWikiModelMigrationCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IWikiEmbeddingVectorStore vectorStore, IMessagePublisher messagePublisher)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _vectorStore = vectorStore;
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task<StartWikiModelMigrationCommandResponse> Handle(StartWikiModelMigrationCommand request, CancellationToken cancellationToken)
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

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以迁移知识库向量化模型.") { StatusCode = 403 };
        }

        if (wiki.EmbeddingModelId == Guid.Empty || wiki.EmbeddingDimensions <= 0)
        {
            throw new BusinessException("知识库尚未配置向量化模型，无需迁移，直接保存设置即可.") { StatusCode = 400 };
        }

        var totalVectorCount = await _vectorStore.CountCollectionAsync(wiki.Id, cancellationToken);
        if (totalVectorCount <= 0)
        {
            throw new BusinessException("知识库暂无向量，无需迁移，重新执行向量化即可.") { StatusCode = 400 };
        }

        // 目标模型须为团队可用口径（团队自有/公开/已授权）且是启用中的 embedding 模型
        var targetModel = await _databaseContext.AiModels.AsNoTracking()
            .Where(m => m.Id == request.ModelId && m.Enabled && m.ModelKind == EmbeddingKind)
            .Select(m => new { m.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (targetModel == null || !await _databaseContext.IsTeamUsableModelAsync(wiki.TeamId, request.ModelId, cancellationToken))
        {
            throw new BusinessException("目标向量化模型不可用（须为该团队可用的 embedding 模型）.") { StatusCode = 400 };
        }

        if (wiki.EmbeddingModelId == request.ModelId && wiki.EmbeddingDimensions == request.Dimensions)
        {
            throw new BusinessException("与当前向量化模型/维度一致，无需迁移.") { StatusCode = 400 };
        }

        var hasActiveTask = await _databaseContext.WorkerTasks.AsNoTracking()
            .AnyAsync(
                x => x.BindType == WikiModelMigrationTask.BindType
                    && x.BindId == wiki.Id
                    && x.IsDeleted == 0
                    && (x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing),
                cancellationToken);
        if (hasActiveTask)
        {
            throw new BusinessException("该知识库已有进行中的迁移任务，请稍后再试.") { StatusCode = 409 };
        }

        var taskData = new WikiModelMigrationTaskData
        {
            TargetModelId = request.ModelId,
            TargetDimensions = request.Dimensions,
            SourceModelId = wiki.EmbeddingModelId,
            SourceDimensions = wiki.EmbeddingDimensions,
            TotalCount = totalVectorCount,
            MigratedCount = 0,
        };

        var task = new WorkerTaskEntity
        {
            Id = Guid.CreateVersion7(),
            BindType = WikiModelMigrationTask.BindType,
            BindId = (int)wiki.Id,
            State = (int)WorkerState.Wait,
            Message = "迁移任务已创建",
            Data = JsonSerializer.Serialize(taskData, WikiModelMigrationTaskData.JsonOptions),
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
                throw new BusinessException("该知识库已有进行中的迁移任务，请稍后再试.") { StatusCode = 409 };
            }

            throw;
        }

        try
        {
            await _messagePublisher.AutoPublishAsync(new WikiModelMigrationMessage { TaskId = task.Id });
        }
        catch (Exception ex)
        {
            task.State = (int)WorkerState.Failed;
            task.Message = ex.Message;
            await _databaseContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new StartWikiModelMigrationCommandResponse { TaskId = task.Id };
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
