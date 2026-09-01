using MediatR;
using MoAI.Settings.Queries;
using MoAI.Settings.Queries.Responses;
using MoAI.Settings.Services;

namespace MoAI.Settings.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySettingsCommand"/>
/// </summary>
public class QuerySettingsCommandHandler : IRequestHandler<QuerySettingsCommand, QuerySettingsCommandResponse>
{
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySettingsCommandHandler"/> class.
    /// </summary>
    /// <param name="settingsService"></param>
    public QuerySettingsCommandHandler(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public Task<QuerySettingsCommandResponse> Handle(QuerySettingsCommand request, CancellationToken cancellationToken)
    {
        return _settingsService.GetSettingsAsync(cancellationToken);
    }
}
