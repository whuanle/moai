using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.WikiCrawler.Commands;

namespace MoAI.Wiki.WikiCrawler.Commands;

/// <summary>
/// <inheritdoc cref="CancalCrawleTaskCommand"/>
/// </summary>
public class CancalCrawleTaskCommandHandler : IRequestHandler<CancalCrawleTaskCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancalCrawleTaskCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CancalCrawleTaskCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CancalCrawleTaskCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.WhereUpdateAsync(
            _databaseContext.WorkerTasks.Where(x => x.BindType == "wiki_crawler" && x.BindId == request.ConfigId),
            x => x.SetProperty(a => a.State, (int)WorkerState.Cancal)
            .SetProperty(a => a.Message, "取消"));

        return EmptyCommandResponse.Default;
    }
}
