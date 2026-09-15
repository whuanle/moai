using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Wiki.External;
using MoAI.Wiki.Queries.Responses;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalWikiDocumentsCommand"/>
/// </summary>
public class QueryExternalWikiDocumentsCommandHandler : IRequestHandler<QueryExternalWikiDocumentsCommand, QueryWikiDocumentsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalWikiDocumentsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryExternalWikiDocumentsCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentsCommandResponse> Handle(QueryExternalWikiDocumentsCommand request, CancellationToken cancellationToken)
    {
        await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var query = _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            query = query.Where(x => x.FileName.Contains(request.Query));
        }

        if (request.IsEmbedding != null)
        {
            query = query.Where(x => x.IsEmbedding == request.IsEmbedding);
        }

        if (request.IncludeFileTypes.Count > 0)
        {
            query = query.Where(x => request.IncludeFileTypes.Contains(x.FileType));
        }

        if (request.ExcludeFileTypes.Count > 0)
        {
            query = query.Where(x => !request.ExcludeFileTypes.Contains(x.FileType));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // 与 KG 外部接口同款：命令已做边界校验，此处再做钳制兜底.
        var pageNo = request.PageNo < 1 ? 1 : request.PageNo;
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var documents = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((pageNo - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var fileIds = documents.Select(x => (long)x.FileId).Distinct().ToArray();
        var files = await _databaseContext.Files
            .Where(x => fileIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = documents.Select(x =>
        {
            files.TryGetValue((long)x.FileId, out var file);
            return new WikiDocumentItem
            {
                DocumentId = x.Id,
                WikiId = x.WikiId,
                FileId = x.FileId,
                FileName = x.FileName,
                FileSize = file?.FileSize ?? 0,
                ContentType = file?.ContentType ?? string.Empty,
                IsEmbedding = x.IsEmbedding,
                CreateUserId = (int)x.CreateUserId,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime
            };
        }).ToList();

        var documentIds = items.Select(x => x.DocumentId).ToArray();
        var chunkCounts = await _databaseContext.WikiDocumentChunkContents
            .Where(x => documentIds.Contains(x.DocumentId))
            .GroupBy(x => x.DocumentId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Count, cancellationToken);

        var metadataCounts = await _databaseContext.WikiDocumentChunkMetadata
            .Where(x => documentIds.Contains(x.DocumentId))
            .GroupBy(x => x.DocumentId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Count, cancellationToken);

        var contentInfos = await _databaseContext.WikiDocumentContents
            .Where(x => documentIds.Contains(x.DocumentId) && x.Content != null)
            .Select(x => new { x.DocumentId, Length = x.Content!.Length })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Length, cancellationToken);

        foreach (var item in items)
        {
            item.ChunkCount = chunkCounts.TryGetValue(item.DocumentId, out var chunkCount) ? chunkCount : 0;
            item.MetadataCount = metadataCounts.TryGetValue(item.DocumentId, out var metadataCount) ? metadataCount : 0;
            item.IsContentExtracted = contentInfos.TryGetValue(item.DocumentId, out var contentLength) && contentLength > 0;
            item.ContentLength = contentInfos.TryGetValue(item.DocumentId, out var length) ? length : 0;
        }

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryWikiDocumentsCommandResponse
        {
            Items = items,
            Total = totalCount,
            PageNo = pageNo,
            PageSize = pageSize
        };
    }
}
