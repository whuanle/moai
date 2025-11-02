using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.WebDocuments.Queries;

public class QueryWikiWebConfigTaskStateListCommandHandler : IRequestHandler<QueryWikiWebConfigTaskStateListCommand, QueryWikiWebConfigTaskStateListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiWebConfigTaskStateListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiWebConfigTaskStateListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiWebConfigTaskStateListCommandResponse> Handle(QueryWikiWebConfigTaskStateListCommand request, CancellationToken cancellationToken)
    {
        var pageStats = await _databaseContext.WikiWebCrawlePageStates
            .OrderByDescending(x => x.CreateTime)
            .Where(x => x.WikiWebConfigId == request.WikiWebConfigId)
            .Select(x => new WikiWebConfigCrawleStateItem
            {
                Id = x.Id,
                CreateTime = x.CreateTime,
                State = (CrawleState)x.State,
                Url = x.Url,
                WikiId = x.WikiId,
                WikiWebConfigId = x.WikiWebConfigId,
                Message = x.Message
            })
            .Take(10).ToArrayAsync();

        var tasks = await _databaseContext.WikiWebCrawleTasks
            .OrderByDescending(x => x.CreateTime)
            .Where(x => x.WikiWebConfigId == request.WikiWebConfigId)
            .Select(x => new WikiConfigCrawleTaskItem
            {
                Id = x.Id,
                CreateTime = x.CreateTime,
                WikiId = x.WikiId,
                WikiWebConfigId = x.WikiWebConfigId,
                CrawleState = (CrawleState)x.CrawleState,
                CreateUserId = x.CreateUserId,
                FaildPageCount = x.FaildPageCount,
                MaxTokensPerParagraph = x.MaxTokensPerParagraph,
                Message = x.Message,
                OverlappingTokens = x.OverlappingTokens,
                PageCount = x.PageCount,
                Tokenizer = x.Tokenizer
            })
            .Take(10).ToArrayAsync();

        return new QueryWikiWebConfigTaskStateListCommandResponse
        {
            PageStates = pageStats,
            Tasks = tasks
        };
    }
}
