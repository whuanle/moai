using MediatR;
using MoAI.Settings.Services;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiUploadLimitCommand"/>
/// </summary>
public class QueryWikiUploadLimitCommandHandler : IRequestHandler<QueryWikiUploadLimitCommand, QueryWikiUploadLimitCommandResponse>
{
    private readonly IWikiSettingsService _wikiSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiUploadLimitCommandHandler"/> class.
    /// </summary>
    /// <param name="wikiSettingsService">知识库设置服务.</param>
    public QueryWikiUploadLimitCommandHandler(IWikiSettingsService wikiSettingsService)
    {
        _wikiSettingsService = wikiSettingsService;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiUploadLimitCommandResponse> Handle(QueryWikiUploadLimitCommand request, CancellationToken cancellationToken)
    {
        var maxFileSizeMb = await _wikiSettingsService.GetMaxFileSizeMbAsync(cancellationToken);

        return new QueryWikiUploadLimitCommandResponse
        {
            MaxFileSizeMb = maxFileSizeMb,
            MaxFileSizeBytes = (long)maxFileSizeMb * 1024 * 1024
        };
    }
}
