using MediatR;
using MoAI.Hangfire.Services;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// <inheritdoc cref="AddWikiPluginRecuringJobCommand"/>
/// </summary>
public class AddWikiPluginRecuringJobCommandHandler : IRequestHandler<AddWikiPluginRecuringJobCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly IRecurringJobService _recurringJobService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiPluginRecuringJobCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="recurringJobService"></param>
    public AddWikiPluginRecuringJobCommandHandler(IMediator mediator, IRecurringJobService recurringJobService)
    {
        _mediator = mediator;
        _recurringJobService = recurringJobService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AddWikiPluginRecuringJobCommand request, CancellationToken cancellationToken)
    {
        if (request.IsStart)
        {
            await _recurringJobService.AddOrUpdateRecurringJobAsync<WikiPluginRecuringJobCommand, AddWikiPluginRecuringJobCommand>(
                key: $"wiki_plugin_{request.WikiId}_{request.ConfigId}",
                cron: request.Cron,
                @params: request);
        }
        else
        {
            await _recurringJobService.RemoveRecurringJobAsync(
                key: $"wiki_plugin_{request.WikiId}_{request.ConfigId}");
        }

        return EmptyCommandResponse.Default;
    }
}
