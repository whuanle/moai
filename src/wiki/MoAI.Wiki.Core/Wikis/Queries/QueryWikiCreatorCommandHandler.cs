using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiCreatorCommandHandler"/>
/// </summary>
public class QueryWikiCreatorCommandHandler : IRequestHandler<QueryWikiCreatorCommand, QueryWikiCreatorCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCreatorCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiCreatorCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCreatorCommandResponse> Handle(QueryWikiCreatorCommand request, CancellationToken cancellationToken)
    {
        var wikiEntity = await _databaseContext.Wikis
            .Where(x => x.Id == request.WikiId)
            .Select(x => new QueryWikiCreatorCommandResponse
            {
                CreatorId = x.CreateUserId,
                WikiId = x.Id,
                IsPublic = x.IsPublic,
                WikiIsExist = x.Id > 0
            }).FirstOrDefaultAsync(cancellationToken);

        return new QueryWikiCreatorCommandResponse
        {
            CreatorId = wikiEntity?.CreatorId ?? 0,
            WikiId = wikiEntity?.WikiId ?? 0,
            IsPublic = wikiEntity?.IsPublic ?? false,
            WikiIsExist = wikiEntity?.WikiIsExist ?? false,
        };
    }
}
