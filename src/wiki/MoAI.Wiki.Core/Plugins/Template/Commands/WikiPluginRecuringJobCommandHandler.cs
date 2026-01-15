using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Hangfire.Models;
using MoAI.Hangfire.Services;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// <inheritdoc cref="WikiPluginRecuringJobCommand"/>
/// </summary>
public class WikiPluginRecuringJobCommandHandler : IRequestHandler<WikiPluginRecuringJobCommand, RecuringJobResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiPluginRecuringJobCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public WikiPluginRecuringJobCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<RecuringJobResponse> Handle(WikiPluginRecuringJobCommand request, CancellationToken cancellationToken)
    {
        var wikiPluginConfigEntity = await _databaseContext.WikiPluginConfigs.FirstOrDefaultAsync(x => x.Id == request.Params.ConfigId && x.WikiId == request.Params.WikiId, cancellationToken);

        if (wikiPluginConfigEntity == null)
        {
            return new RecuringJobResponse
            {
                IsCancel = true
            };
        }

        if (wikiPluginConfigEntity.PluginType == "crawler")
        {
            await _mediator.Send(
                new StartWikiCrawlerPluginTaskCommand
                {
                    WikiId = request.Params.WikiId,
                    ConfigId = request.Params.ConfigId,
                    IsAutoProcess = request.Params.IsAutoProcess,
                    IsStart = true,
                    AutoProcessConfig = request.Params.AutoProcessConfig
                },
                cancellationToken);
        }
        else if (wikiPluginConfigEntity.PluginType == "feishu")
        {
            await _mediator.Send(
                new StartWikiFeishuPluginTaskCommand
                {
                    WikiId = request.Params.WikiId,
                    ConfigId = request.Params.ConfigId,
                    IsAutoProcess = request.Params.IsAutoProcess,
                    IsStart = true,
                    AutoProcessConfig = request.Params.AutoProcessConfig
                },
                cancellationToken);
        }

        return new RecuringJobResponse
        {
            IsCancel = false
        };
    }
}
