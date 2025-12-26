using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.User.Queries;
using MoAI.Wiki.DocumentManager.Queries;
using MoAI.Wiki.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询知识库文档列表.
/// </summary>
public class QueryWikiDocumentListCommandHandler : IRequestHandler<QueryWikiDocumentListCommand, QueryWikiDocumentListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiDocumentListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentListCommandResponse> Handle(QueryWikiDocumentListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.WikiDocuments.Where(x => x.WikiId == request.WikiId);

        if (!string.IsNullOrEmpty(request.Query))
        {
            query = query.Where(x => x.FileName.Contains(request.Query));
        }

        if (request.IncludeFileTypes != null && request.IncludeFileTypes.Count > 0)
        {
            query = query.Where(x => request.IncludeFileTypes.Contains(x.FileType));
        }

        if (request.ExcludeFileTypes != null && request.ExcludeFileTypes.Count > 0)
        {
            query = query.Where(x => !request.ExcludeFileTypes.Contains(x.FileType));
        }

        var totalCount = await query.CountAsync();

        var result = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip(request.Skip)
            .Take(request.Take)
            .Join(_databaseContext.Files.Where(x => x.IsUploaded), a => a.FileId, b => b.Id, (a, b) => new QueryWikiDocumentListItem
        {
            DocumentId = a.Id,
            FileName = a.FileName,
            FileSize = b.FileSize,
            ContentType = b.ContentType,
            CreateTime = a.CreateTime,
            CreateUserId = a.CreateUserId,
            UpdateTime = a.UpdateTime,
            UpdateUserId = a.UpdateUserId,
            Embedding = a.IsEmbedding
        }).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = result
        });

        return new QueryWikiDocumentListCommandResponse
        {
            Total = totalCount,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
            Items = result
        };
    }
}
