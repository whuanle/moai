using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.External;
using MoAI.Wiki.Queries.Responses;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalDocumentEmbeddingCommand"/>
/// </summary>
public class QueryExternalDocumentEmbeddingCommandHandler : IRequestHandler<QueryExternalDocumentEmbeddingCommand, QueryWikiDocumentEmbeddingCommandResponse>
{
    private const string EmbeddingTaskBindType = "embedding";

    /// <summary>
    /// detail 响应中 <see cref="QueryWikiDocumentEmbeddingCommandResponse.Content"/> 只返回的预览字符上限.
    /// 超出部分不随 detail 传输，由 /content 接口取全文（与内部 detail 接口保持一致）.
    /// </summary>
    private const int ContentPreviewLimit = 10_000;
    private static readonly int[] ActiveTaskStates = { (int)WorkerState.Wait, (int)WorkerState.Processing };

    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly IWikiEmbeddingVectorStore _vectorStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalDocumentEmbeddingCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    public QueryExternalDocumentEmbeddingCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer, IWikiEmbeddingVectorStore vectorStore)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _vectorStore = vectorStore;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentEmbeddingCommandResponse> Handle(QueryExternalDocumentEmbeddingCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId && x.WikiId == request.WikiId, cancellationToken);
        if (document == null)
        {
            throw new BusinessException("知识库文档不存在.") { StatusCode = 404 };
        }

        var embeddingModelName = await _databaseContext.AiModels
            .Where(x => x.Id == wiki.EmbeddingModelId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var chunks = await _databaseContext.WikiDocumentChunkContents
            .Where(x => x.DocumentId == document.Id && x.WikiId == wiki.Id)
            .OrderBy(x => x.SliceOrder)
            .ToListAsync(cancellationToken);

        var chunkIds = chunks.Select(x => x.Id).ToArray();
        var metadataByChunk = await _databaseContext.WikiDocumentChunkMetadata
            .Where(x => chunkIds.Contains(x.ChunkId))
            .GroupBy(x => x.ChunkId)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Select(m => new WikiDocumentChunkMetadataItem
                {
                    MetadataType = m.MetadataType,
                    MetadataContent = m.MetadataContent,
                }).ToList(),
                cancellationToken);

        var embeddingCount = await _vectorStore.CountDocumentVectorsAsync(wiki.Id, document.Id, cancellationToken);

        var content = await _databaseContext.WikiDocumentContents
            .FirstOrDefaultAsync(x => x.DocumentId == document.Id && x.WikiId == wiki.Id, cancellationToken);

        var (chunkSize, chunkOverlap, splitMode, overlapUnit, sizeUnit, tokenEncodingOrModel) = ReadSliceConfig(document.SliceConfig);

        var latestTask = await QueryLatestEmbeddingTaskAsync(document.Id, cancellationToken);

        var items = chunks.Select(x => new WikiDocumentEmbeddingChunkItem
        {
            ChunkId = x.Id,
            SliceOrder = x.SliceOrder,
            SliceContent = x.SliceContent,
            MetadataCount = metadataByChunk.TryGetValue(x.Id, out var mc) ? mc.Count : 0,
            Metadatas = metadataByChunk.TryGetValue(x.Id, out var ml) ? ml : new List<WikiDocumentChunkMetadataItem>(),
        }).ToList();

        var fullContent = content?.Content ?? string.Empty;
        var preview = fullContent.Length <= ContentPreviewLimit ? fullContent : fullContent[..ContentPreviewLimit];

        return new QueryWikiDocumentEmbeddingCommandResponse
        {
            WikiId = wiki.Id,
            WikiName = wiki.Name,
            EmbeddingModelId = wiki.EmbeddingModelId,
            EmbeddingModelName = embeddingModelName,
            EmbeddingDimensions = wiki.EmbeddingDimensions,
            IsLock = wiki.IsLock,
            SplitMode = splitMode,
            ChunkSize = chunkSize,
            ChunkOverlap = chunkOverlap,
            OverlapUnit = overlapUnit,
            SizeUnit = sizeUnit,
            TokenEncodingOrModel = tokenEncodingOrModel,
            DocumentId = document.Id,
            FileName = document.FileName,
            TaskId = latestTask?.Id,
            TaskState = latestTask?.State,
            TaskMessage = latestTask?.Message,
            IsEmbedding = document.IsEmbedding,
            EmbeddingCount = embeddingCount,
            IsContentExtracted = content != null && !string.IsNullOrWhiteSpace(fullContent),
            ContentLength = fullContent.Length,
            // 惰性加载：detail 只回传预览（前 ContentPreviewLimit 字），完整内容走 /content 接口获取。
            Content = preview,
            ContentPreviewLength = preview.Length,
            Items = items,
        };
    }

    private async Task<WorkerTaskView?> QueryLatestEmbeddingTaskAsync(int documentId, CancellationToken cancellationToken)
    {
        var baseQuery = _databaseContext.WorkerTasks
            .AsNoTracking()
            .Where(x => x.BindType == EmbeddingTaskBindType && x.BindId == documentId)
            .Select(x => new WorkerTaskView
            {
                Id = x.Id,
                State = x.State,
                Message = x.Message,
                CreateTime = x.CreateTime,
            });

        if (string.Equals(_databaseContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            // SQLite in tests cannot translate DateTimeOffset OrderBy reliably.
            var taskCandidates = await baseQuery.ToListAsync(cancellationToken);
            return taskCandidates
                .Where(x => ActiveTaskStates.Contains(x.State))
                .OrderByDescending(x => x.CreateTime)
                .ThenBy(x => x.Id)
                .FirstOrDefault()
                ?? taskCandidates
                    .Where(x => !ActiveTaskStates.Contains(x.State))
                    .OrderByDescending(x => x.CreateTime)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();
        }


        // Production path: keep selection in DB and fetch only top1.
        var activeTask = await baseQuery
            .Where(x => ActiveTaskStates.Contains(x.State))
            .OrderByDescending(x => x.CreateTime)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeTask != null)
        {
            return activeTask;
        }

        return await baseQuery
            .Where(x => !ActiveTaskStates.Contains(x.State))
            .OrderByDescending(x => x.CreateTime)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed class WorkerTaskView
    {
        public Guid Id { get; set; }

        public int State { get; set; }

        public string Message { get; set; } = string.Empty;

        public DateTimeOffset CreateTime { get; set; }
    }

    private static (int ChunkSize, int ChunkOverlap, string SplitMode, string OverlapUnit, string SizeUnit, string? TokenEncodingOrModel) ReadSliceConfig(string? sliceConfig)
    {
        if (string.IsNullOrWhiteSpace(sliceConfig))
        {
            return (0, 0, "markdown", "character", "character", null);
        }

        try
        {
            using var doc = JsonDocument.Parse(sliceConfig);
            var root = doc.RootElement;
            var chunkSize = root.TryGetProperty("chunkSize", out var cs) && cs.TryGetInt32(out var csv) ? csv : 0;
            var chunkOverlap = root.TryGetProperty("chunkOverlap", out var co) && co.TryGetInt32(out var cov) ? cov : 0;
            var splitMode = root.TryGetProperty("splitMode", out var sm) && sm.ValueKind == JsonValueKind.String ? sm.GetString() ?? "markdown" : "markdown";
            var overlapUnit = root.TryGetProperty("overlapUnit", out var ou) && ou.ValueKind == JsonValueKind.String ? ou.GetString() ?? "character" : "character";
            var sizeUnit = root.TryGetProperty("sizeUnit", out var su) && su.ValueKind == JsonValueKind.String ? su.GetString() ?? "character" : "character";
            var tokenEncodingOrModel = root.TryGetProperty("tokenEncodingOrModel", out var tem) && tem.ValueKind == JsonValueKind.String ? tem.GetString() : null;
            return (chunkSize, chunkOverlap, splitMode, overlapUnit, sizeUnit, tokenEncodingOrModel);
        }
        catch
        {
            return (0, 0, "markdown", "character", "character", null);
        }
    }
}
