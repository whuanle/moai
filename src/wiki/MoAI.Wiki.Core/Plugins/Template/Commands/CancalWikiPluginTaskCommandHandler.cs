using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Commands;

namespace MoAI.Wiki.Template.Commands;

/// <summary>
/// <inheritdoc cref="CancalWikiPluginTaskCommand"/>
/// </summary>
public class CancalWikiPluginTaskCommandHandler : IRequestHandler<CancalWikiPluginTaskCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancalWikiPluginTaskCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CancalWikiPluginTaskCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CancalWikiPluginTaskCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.WhereUpdateAsync(
            _databaseContext.WorkerTasks.Where(x => x.BindType == "wiki" && x.BindId == request.ConfigId),
            x => x.SetProperty(a => a.State, (int)WorkerState.Cancal)
            .SetProperty(a => a.Message, "取消"));

        return EmptyCommandResponse.Default;
    }
}
