using System;
using System.Text.Json;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;
using Npgsql;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="EmbedExternalDocumentCommand"/>
/// </summary>
public class EmbedExternalDocumentCommandHandler : IRequestHandler<EmbedExternalDocumentCommand, EmbeddingDocumentCommandResponse>
{
    private const string EmbeddingBindType = "embedding";
    private const string ActiveTaskConstraintName = "ux_worker_task_bind_active";

    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbedExternalDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    public EmbedExternalDocumentCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer, IMessagePublisher messagePublisher)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task<EmbeddingDocumentCommandResponse> Handle(EmbedExternalDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!request.IsEmbedSourceText && !request.IsEmbedMetadata)
        {
            throw new BusinessException("至少需要选择一种向量化内容（原文或元数据）。") { StatusCode = 400 };
        }

        var wiki = await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        if (wiki.EmbeddingModelId == Guid.Empty || wiki.EmbeddingDimensions <= 0)
        {
            throw new BusinessException("知识库未配置向量化模型或维度，请先完成配置.") { StatusCode = 409 };
        }

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId && x.WikiId == request.WikiId, cancellationToken);
        if (document == null)
        {
            throw new BusinessException("知识库文档不存在.") { StatusCode = 404 };
        }

        var documentContents = await _databaseContext.WikiDocumentContents
            .Where(x => x.DocumentId == document.Id && x.WikiId == document.WikiId)
            .Select(x => x.Content)
            .ToListAsync(cancellationToken);
        var hasContent = documentContents.Any(content => !string.IsNullOrWhiteSpace(content));
        if (!hasContent)
        {
            throw new BusinessException("文档尚未提取内容，请先执行内容提取.") { StatusCode = 409 };
        }

        if (request.IsEmbedSourceText)
        {
            var hasChunks = await _databaseContext.WikiDocumentChunkContents
                .AnyAsync(x => x.DocumentId == document.Id && x.WikiId == document.WikiId, cancellationToken);
            if (!hasChunks)
            {
                throw new BusinessException("文档尚未切割，请先执行文档切割.") { StatusCode = 409 };
            }
        }

        if (request.IsEmbedMetadata)
        {
            var hasMetadata = await _databaseContext.WikiDocumentChunkMetadata
                .Join(
                    _databaseContext.WikiDocumentChunkContents,
                    metadata => metadata.ChunkId,
                    chunk => chunk.Id,
                    (metadata, chunk) => new { Metadata = metadata, Chunk = chunk })
                .AnyAsync(
                    x => x.Metadata.DocumentId == document.Id
                         && x.Metadata.WikiId == document.WikiId
                         && x.Chunk.DocumentId == document.Id
                         && x.Chunk.WikiId == document.WikiId,
                    cancellationToken);
            if (!hasMetadata)
            {
                throw new BusinessException("文档尚无可用元数据，请先生成切片元数据.") { StatusCode = 409 };
            }
        }

        var hasActiveTask = await _databaseContext.WorkerTasks
            .AnyAsync(
                x => x.BindType == EmbeddingBindType
                    && x.BindId == document.Id
                    && (x.State == (int)WorkerState.Wait || x.State == (int)WorkerState.Processing),
                cancellationToken);
        if (hasActiveTask)
        {
            throw new BusinessException("文档已有进行中的向量化任务，请稍后再试.") { StatusCode = 409 };
        }

        var taskData = new WikiDocumentEmbeddingTaskData
        {
            WikiId = document.WikiId,
            DocumentId = document.Id,
            IsEmbedSourceText = request.IsEmbedSourceText,
            IsEmbedMetadata = request.IsEmbedMetadata,
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
                throw new BusinessException("文档已有进行中的向量化任务，请稍后再试.") { StatusCode = 409 };
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
            throw;
        }

        return new EmbeddingDocumentCommandResponse { TaskId = task.Id };
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
