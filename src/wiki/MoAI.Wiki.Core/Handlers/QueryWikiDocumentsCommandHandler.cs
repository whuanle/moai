using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiDocumentsCommand"/>
/// </summary>
public class QueryWikiDocumentsCommandHandler : IRequestHandler<QueryWikiDocumentsCommand, QueryWikiDocumentsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryWikiDocumentsCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentsCommandResponse> Handle(QueryWikiDocumentsCommand request, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(request.WikiId, cancellationToken);

        var query = _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && x.IsDeleted == 0);

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

        var documents = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip(request.Skip)
            .Take(request.Take)
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
            PageNo = request.PageNo,
            PageSize = request.PageSize
        };
    }

    private async Task EnsureMemberAsync(long wikiId, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.FirstOrDefaultAsync(x => x.Id == wikiId && x.IsDeleted == 0, cancellationToken);
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
    }
}
