using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;
using Npgsql;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="BatchRunWikiDocumentWorkflowCommand"/>
/// </summary>
public class BatchRunWikiDocumentWorkflowCommandHandler : IRequestHandler<BatchRunWikiDocumentWorkflowCommand, BatchRunWikiDocumentWorkflowCommandResponse>
{
    private const string EmbeddingBindType = "embedding";
    private const string ActiveTaskConstraintName = "ux_worker_task_bind_active";

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;
    private readonly WikiDocumentProcessingService _processingService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<BatchRunWikiDocumentWorkflowCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchRunWikiDocumentWorkflowCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="processingService">文档内容处理服务（提取/切割）.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public BatchRunWikiDocumentWorkflowCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IUserContextProvider userContextProvider,
        WikiDocumentProcessingService processingService,
        IMessagePublisher messagePublisher,
        ILogger<BatchRunWikiDocumentWorkflowCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
        _processingService = processingService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BatchRunWikiDocumentWorkflowCommandResponse> Handle(BatchRunWikiDocumentWorkflowCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (request.IsEmbedding && (wiki.EmbeddingModelId == Guid.Empty || wiki.EmbeddingDimensions <= 0))
        {
            throw new BusinessException("知识库未配置向量化模型或维度，请先完成配置.") { StatusCode = 409 };
        }

        if (request.IsGenerateMetadata)
        {
            await EnsureConversationModelAsync(request.MetadataModelId, wiki.TeamId, "元数据生成", cancellationToken);
        }

        if (request.IsAiPartition)
        {
            await EnsureConversationModelAsync(request.AiModelId, wiki.TeamId, "智能切割", cancellationToken);
        }

        var strategyTypes = request.StrategyTypes?.Distinct().ToList();

        var requestedIds = request.DocumentIds.Distinct().ToList();
        var documentIds = requestedIds.Select(x => (int)x).ToList();
        var documents = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && documentIds.Contains(x.Id) && x.IsDeleted == 0)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var hasContentIds = await LoadContentStateAsync(wiki.Id, documentIds, cancellationToken);
        var hasChunkIds = await LoadChunkStateAsync(wiki.Id, documentIds, cancellationToken);
        var hasMetadataIds = await LoadMetadataStateAsync(wiki.Id, documentIds, cancellationToken);
        var activeTaskIds = await _databaseContext.WorkerTasks
            .Where(x => x.BindType == EmbeddingBindType
                && documentIds.Contains(x.BindId)
                && x.IsDeleted == 0
                && (x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing))
            .Select(x => x.BindId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var items = new List<BatchRunWikiDocumentWorkflowDocumentItem>();
        foreach (var requestedId in requestedIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documentId = (int)requestedId;

            if (!documents.TryGetValue(documentId, out var document))
            {
                items.Add(Fail(requestedId, string.Empty, "文档不存在或已删除."));
                continue;
            }

            if (activeTaskIds.Contains(documentId))
            {
                items.Add(Fail(requestedId, document.FileName, "文档已有进行中的任务，请稍后再试."));
                continue;
            }

            var hasContent = hasContentIds.Contains(documentId);
            var hasChunks = hasChunkIds.Contains(documentId);
            var hasMetadata = hasMetadataIds.Contains(documentId);
            var partitionInTask = request.IsPartition && request.IsAiPartition;

            if (request.IsPartition)
            {
                try
                {
                    if (!hasContent)
                    {
                        await _processingService.ExtractAsync(wiki.Id, documentId, cancellationToken);
                    }

                    if (!request.IsAiPartition)
                    {
                        // 普通切割本地切分，同步执行；AI 切割为 LLM 调用，随异步任务执行
                        await _processingService.PartitionAsync(
                            wiki.Id,
                            documentId,
                            request.ChunkSize,
                            request.ChunkOverlap,
                            request.SplitMode,
                            request.OverlapUnit,
                            request.SizeUnit,
                            request.TokenEncodingOrModel,
                            cancellationToken);
                        hasChunks = true;
                    }

                    hasContent = true;
                    hasMetadata = false;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "批量工作流切割前置执行失败. WikiId={WikiId}, DocumentId={DocumentId}", wiki.Id, documentId);
                    items.Add(Fail(requestedId, document.FileName, $"切割失败：{ex.Message}"));
                    continue;
                }
            }

            if (request.IsGenerateMetadata && !hasChunks && !partitionInTask)
            {
                items.Add(Fail(requestedId, document.FileName, "文档尚未切割，请先执行文档切割（或同时勾选切割步骤）."));
                continue;
            }

            if (request.IsEmbedding)
            {
                if (!hasContent)
                {
                    items.Add(Fail(requestedId, document.FileName, "文档尚未提取内容，请先执行内容提取（或同时勾选切割步骤）."));
                    continue;
                }

                if (request.EmbedSourceText && !hasChunks && !partitionInTask)
                {
                    items.Add(Fail(requestedId, document.FileName, "文档尚未切割，请先执行文档切割（或同时勾选切割步骤）."));
                    continue;
                }

                if (request.EmbedMetadata && !request.IsGenerateMetadata && (partitionInTask || !hasMetadata))
                {
                    items.Add(Fail(requestedId, document.FileName, partitionInTask
                        ? "重新切割会清空已有元数据，请同时勾选生成元数据步骤或取消元数据向量化."
                        : "文档尚无可用元数据，请先生成切片元数据（或同时勾选元数据步骤）."));
                    continue;
                }
            }

            if (!request.IsGenerateMetadata && !request.IsEmbedding && !partitionInTask)
            {
                items.Add(new BatchRunWikiDocumentWorkflowDocumentItem
                {
                    DocumentId = requestedId,
                    FileName = document.FileName,
                    Success = true,
                    Message = "切割完成",
                });
                continue;
            }

            var task = await EnqueueWorkflowTaskAsync(wiki.Id, document, request, strategyTypes, cancellationToken);
            if (!task.Success)
            {
                items.Add(Fail(requestedId, document.FileName, task.Message));
                continue;
            }

            var stepText = (request.IsAiPartition, request.IsGenerateMetadata, request.IsEmbedding) switch
            {
                (true, true, true) => "已提交 AI 切割、元数据生成与向量化任务",
                (true, true, false) => "已提交 AI 切割与元数据生成任务",
                (true, false, true) => "已提交 AI 切割与向量化任务",
                (true, false, false) => "已提交 AI 切割任务",
                (false, true, true) => "已提交元数据生成与向量化任务",
                (false, true, false) => "已提交元数据生成任务",
                _ => "已提交向量化任务",
            };

            items.Add(new BatchRunWikiDocumentWorkflowDocumentItem
            {
                DocumentId = requestedId,
                FileName = document.FileName,
                Success = true,
                Message = stepText,
                TaskId = task.TaskId,
            });
        }

        return new BatchRunWikiDocumentWorkflowCommandResponse { Items = items };
    }

    private async Task<(bool Success, string Message, Guid? TaskId)> EnqueueWorkflowTaskAsync(
        int wikiId,
        WikiDocumentEntity document,
        BatchRunWikiDocumentWorkflowCommand request,
        List<MetadataGenerationStrategy>? strategyTypes,
        CancellationToken cancellationToken)
    {
        var taskData = new WikiDocumentEmbeddingTaskData
        {
            WikiId = wikiId,
            DocumentId = document.Id,
            IsEmbedSourceText = request.IsEmbedding && request.EmbedSourceText,
            IsEmbedMetadata = request.IsEmbedding && request.EmbedMetadata,
            AiPartitionModelId = request.IsAiPartition ? request.AiModelId : Guid.Empty,
            AiPartitionPromptTemplate = request.IsAiPartition ? request.PromptTemplate : null,
            MetadataModelId = request.IsGenerateMetadata ? request.MetadataModelId : Guid.Empty,
            MetadataStrategyTypes = request.IsGenerateMetadata ? strategyTypes : null,
        };

        var task = new WorkerTaskEntity
        {
            Id = Guid.CreateVersion7(),
            BindType = EmbeddingBindType,
            BindId = document.Id,
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
            _logger.LogError(ex, "批量工作流任务发布失败. WikiId={WikiId}, DocumentId={DocumentId}", wikiId, document.Id);
            return (false, $"任务提交失败：{ex.Message}", null);
        }

        return (true, string.Empty, task.Id);
    }

    private async Task<HashSet<int>> LoadContentStateAsync(int wikiId, List<int> documentIds, CancellationToken cancellationToken)
    {
        var ids = await _databaseContext.WikiDocumentContents
            .Where(x => x.WikiId == wikiId && documentIds.Contains(x.DocumentId) && x.IsDeleted == 0 && x.Content != null && x.Content != string.Empty)
            .Select(x => x.DocumentId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private async Task<HashSet<int>> LoadChunkStateAsync(int wikiId, List<int> documentIds, CancellationToken cancellationToken)
    {
        var ids = await _databaseContext.WikiDocumentChunkContents
            .Where(x => x.WikiId == wikiId && documentIds.Contains(x.DocumentId) && x.IsDeleted == 0)
            .Select(x => x.DocumentId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private async Task<HashSet<int>> LoadMetadataStateAsync(int wikiId, List<int> documentIds, CancellationToken cancellationToken)
    {
        var ids = await _databaseContext.WikiDocumentChunkMetadata
            .Where(x => x.WikiId == wikiId && documentIds.Contains(x.DocumentId) && x.IsDeleted == 0)
            .Select(x => x.DocumentId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private async Task EnsureConversationModelAsync(Guid modelId, int teamId, string purpose, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == modelId && x.Enabled && x.IsDeleted == 0, cancellationToken);
        if (model == null)
        {
            throw new BusinessException($"{purpose}模型不存在或未启用.") { StatusCode = 404 };
        }

        if (!string.Equals(model.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException($"所选模型不是对话模型，无法用于{purpose}.") { StatusCode = 400 };
        }

        if (model.IsPublic)
        {
            return;
        }

        var authorized = await _databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
        if (!authorized)
        {
            throw new BusinessException($"{purpose}模型未授权给你的团队使用.") { StatusCode = 403 };
        }
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

    private static BatchRunWikiDocumentWorkflowDocumentItem Fail(long documentId, string fileName, string message)
    {
        return new BatchRunWikiDocumentWorkflowDocumentItem
        {
            DocumentId = documentId,
            FileName = fileName,
            Success = false,
            Message = message,
        };
    }
}
