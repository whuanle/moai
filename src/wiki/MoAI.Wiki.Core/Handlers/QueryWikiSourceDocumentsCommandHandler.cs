using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.Wiki.Models;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiSourceDocumentsCommand"/>
/// </summary>
public class QueryWikiSourceDocumentsCommandHandler : IRequestHandler<QueryWikiSourceDocumentsCommand, QueryWikiSourceDocumentsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiSourceDocumentsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryWikiSourceDocumentsCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiSourceDocumentsCommandResponse> Handle(QueryWikiSourceDocumentsCommand request, CancellationToken cancellationToken)
    {
        var source = await _databaseContext.WikiSources
            .FirstOrDefaultAsync(x => x.Id == request.SourceId && x.WikiId == request.WikiId, cancellationToken);

        if (source == null)
        {
            throw new BusinessException("外部源不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(source.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.WikiSourceDocuments.Where(x => x.SourceId == source.Id);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var keyword = request.Query;
            query = query.Where(x => x.ExternalTitle.Contains(keyword) || x.ExternalPath.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);

        var documents = await query
            .OrderByDescending(x => x.LastSyncTime ?? x.CreateTime)
            .ThenBy(x => x.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        var documentIds = documents.Select(x => x.DocumentId).ToArray();
        var fileNames = documentIds.Length == 0
            ? new System.Collections.Generic.Dictionary<int, string>()
            : await _databaseContext.WikiDocuments
                .Where(x => documentIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FileName, cancellationToken);

        var items = documents.Select(x => new WikiSourceDocumentItem
        {
            SourceId = x.SourceId,
            ExternalKey = x.ExternalKey,
            ExternalTitle = x.ExternalTitle,
            ExternalPath = x.ExternalPath,
            DocumentId = x.DocumentId,
            FileName = fileNames.TryGetValue(x.DocumentId, out var fileName) ? fileName : string.Empty,
            Status = (WikiSourceDocumentStatus)x.Status,
            LastSyncTime = x.LastSyncTime,
            LastError = x.LastError ?? string.Empty,
            Revision = x.Revision ?? string.Empty,
        }).ToList();

        return new QueryWikiSourceDocumentsCommandResponse
        {
            Total = total,
            Items = items,
        };
    }
}
