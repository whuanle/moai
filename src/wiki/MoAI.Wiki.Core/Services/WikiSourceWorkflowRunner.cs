using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Maomi.MQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using Npgsql;

namespace MoAI.Wiki.Services;

/// <summary>
/// 外部源文档工作流执行器：与知识库「批量处理」同一策略（切割 / 生成元数据 / 向量化三步可自由组合），
/// 供外部源在文档新建或内容变化时触发。普通切割在调用方同步执行，AI 切割、元数据生成与向量化随后台任务执行.
/// </summary>
[InjectOnScoped]
public class WikiSourceWorkflowRunner
{
    private const string EmbeddingBindType = "embedding";
    private const string ActiveTaskConstraintName = "ux_worker_task_bind_active";

    private readonly DatabaseContext _databaseContext;
    private readonly WikiDocumentProcessingService _processingService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<WikiSourceWorkflowRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSourceWorkflowRunner"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="processingService">文档内容处理服务（提取/切割）.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public WikiSourceWorkflowRunner(
        DatabaseContext databaseContext,
        WikiDocumentProcessingService processingService,
        IMessagePublisher messagePublisher,
        ILogger<WikiSourceWorkflowRunner> logger)
    {
        _databaseContext = databaseContext;
        _processingService = processingService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <summary>
    /// 按外部源工作流配置处理单个文档.
    /// </summary>
    /// <param name="wiki">知识库实体.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="config">外部源工作流配置，为空表示不执行工作流（仅同步文档内容）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否成功、结果说明与后台任务 id.</returns>
    public async Task<(bool Success, string Message, Guid? TaskId)> RunAsync(
        WikiEntity wiki,
        int documentId,
        WikiWorkflowConfig? config,
        CancellationToken cancellationToken = default)
    {
        // 外部源未单独配置工作流时，回退知识库默认工作流预设
        var workflow = config ?? WikiWorkflowConfigJson.Deserialize(wiki.DefaultWorkflowConfig);
        if (workflow == null)
        {
            return (true, "未配置工作流，仅同步文档内容", null);
        }

        var partition = workflow.Partition;
        var metadata = workflow.Metadata;
        var embedding = workflow.Embedding;
        var useAiPartition = partition != null && partition.Mode == WorkflowPartitionMode.Ai;

        if (partition != null && !useAiPartition)
        {
            try
            {
                await EnsureContentAsync(wiki.Id, documentId, cancellationToken);
                await _processingService.PartitionAsync(
                    wiki.Id,
                    documentId,
                    partition.ChunkSize,
                    partition.ChunkOverlap,
                    partition.SplitMode,
                    partition.OverlapUnit,
                    partition.SizeUnit,
                    partition.TokenEncodingOrModel,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "外部源工作流切割失败. WikiId={WikiId}, DocumentId={DocumentId}", wiki.Id, documentId);
                return (false, $"切割失败：{ex.Message}", null);
            }
        }

        if (useAiPartition)
        {
            var (ok, message) = await EnsureConversationModelAsync(partition!.AiModelId, wiki.TeamId, "智能切割", cancellationToken);
            if (!ok)
            {
                return (false, message, null);
            }
        }

        if (metadata != null)
        {
            var (ok, message) = await EnsureConversationModelAsync(metadata.MetadataModelId, wiki.TeamId, "元数据生成", cancellationToken);
            if (!ok)
            {
                return (false, message, null);
            }
        }

        if (embedding != null && (wiki.EmbeddingModelId == Guid.Empty || wiki.EmbeddingDimensions <= 0))
        {
            return (false, "知识库未配置向量化模型或维度，请先完成配置.", null);
        }

        // 只勾选普通切割时无需后台任务
        if (!useAiPartition && metadata == null && embedding == null)
        {
            return (true, "切割完成", null);
        }

        return await EnqueueAsync(wiki, documentId, workflow, cancellationToken);
    }

    private async Task EnsureContentAsync(int wikiId, int documentId, CancellationToken cancellationToken)
    {
        var hasContent = await _databaseContext.WikiDocumentContents
            .AnyAsync(x => x.WikiId == wikiId && x.DocumentId == documentId && x.IsDeleted == 0 && x.Content != null && x.Content != string.Empty, cancellationToken);

        if (!hasContent)
        {
            await _processingService.ExtractAsync(wikiId, documentId, cancellationToken);
        }
    }

    private async Task<(bool Success, string Message, Guid? TaskId)> EnqueueAsync(
        WikiEntity wiki,
        int documentId,
        WikiWorkflowConfig workflow,
        CancellationToken cancellationToken)
    {
        var partition = workflow.Partition;
        var metadata = workflow.Metadata;
        var embedding = workflow.Embedding;
        var useAiPartition = partition != null && partition.Mode == WorkflowPartitionMode.Ai;

        var taskData = new WikiDocumentEmbeddingTaskData
        {
            WikiId = wiki.Id,
            DocumentId = documentId,
            IsEmbedSourceText = embedding?.EmbedSourceText ?? false,
            IsEmbedMetadata = embedding?.EmbedMetadata ?? false,
            AiPartitionModelId = useAiPartition ? partition!.AiModelId : Guid.Empty,
            AiPartitionPromptTemplate = useAiPartition ? partition!.PromptTemplate : null,
            MetadataModelId = metadata?.MetadataModelId ?? Guid.Empty,
            MetadataStrategyTypes = metadata?.StrategyTypes?.Distinct().ToList(),
        };

        var task = new WorkerTaskEntity
        {
            Id = Guid.CreateVersion7(),
            BindType = EmbeddingBindType,
            BindId = documentId,
            State = (int)WorkerState.Wait,
            Message = "任务已创建",
            Data = JsonSerializer.Serialize(taskData),
        };

        await _databaseContext.WorkerTasks.AddAsync(task, cancellationToken);
        try
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (IsActiveTaskUniqueConstraintViolation(ex))
            {
                return (false, "文档已有进行中的任务，请稍后再试.", null);
            }

            throw;
        }

        try
        {
            await _messagePublisher.AutoPublishAsync(new EmbeddingDocumentTaskMessage { TaskId = task.Id });
        }
        catch (Exception ex)
        {
            task.State = (int)WorkerState.Failed;
            task.Message = ex.Message;
            await _databaseContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "外部源工作流任务发布失败. WikiId={WikiId}, DocumentId={DocumentId}", wiki.Id, documentId);
            return (false, $"任务提交失败：{ex.Message}", null);
        }

        var stepText = (useAiPartition, metadata != null, embedding != null) switch
        {
            (true, true, true) => "已提交 AI 切割、元数据生成与向量化任务",
            (true, true, false) => "已提交 AI 切割与元数据生成任务",
            (true, false, true) => "已提交 AI 切割与向量化任务",
            (true, false, false) => "已提交 AI 切割任务",
            (false, true, true) => "已提交元数据生成与向量化任务",
            (false, true, false) => "已提交元数据生成任务",
            _ => "已提交向量化任务",
        };

        return (true, stepText, task.Id);
    }

    private async Task<(bool Success, string Message)> EnsureConversationModelAsync(Guid modelId, int teamId, string purpose, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == modelId && x.Enabled && x.IsDeleted == 0, cancellationToken);

        if (model == null)
        {
            return (false, $"{purpose}模型不存在或未启用.");
        }

        if (!string.Equals(model.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"所选模型不是对话模型，无法用于{purpose}.");
        }

        if (model.IsPublic)
        {
            return (true, string.Empty);
        }

        var authorized = await _databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);

        return authorized ? (true, string.Empty) : (false, $"{purpose}模型未授权给你的团队使用.");
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
