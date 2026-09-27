using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalWikiRecallCommand"/>
/// </summary>
public class QueryExternalWikiRecallCommandHandler : IRequestHandler<QueryExternalWikiRecallCommand, QueryExternalWikiRecallCommandResponse>
{
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;
    private readonly IWikiSearchService _wikiSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalWikiRecallCommandHandler"/> class.
    /// </summary>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    /// <param name="wikiSearchService">知识库检索服务.</param>
    public QueryExternalWikiRecallCommandHandler(IExternalWikiAuthorizer externalWikiAuthorizer, IWikiSearchService wikiSearchService)
    {
        _externalWikiAuthorizer = externalWikiAuthorizer;
        _wikiSearchService = wikiSearchService;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalWikiRecallCommandResponse> Handle(QueryExternalWikiRecallCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var hits = await _wikiSearchService.SearchInWikiAsync(
            wiki.Id,
            request.Query,
            request.Top,
            request.MinScore,
            request.DocumentIds.Count > 0 ? request.DocumentIds : null,
            cancellationToken);

        return new QueryExternalWikiRecallCommandResponse
        {
            WikiId = wiki.Id,
            WikiName = wiki.Name,
            Query = request.Query,
            Items = hits.Select(x => new QueryExternalWikiRecallItem
            {
                DocumentId = x.DocumentId,
                DocumentName = x.DocumentName,
                ChunkId = x.ChunkId,
                MetadataType = x.MetadataType,
                Content = x.Content,
                Score = x.Score ?? 0,
            }).ToList(),
        };
    }
}
