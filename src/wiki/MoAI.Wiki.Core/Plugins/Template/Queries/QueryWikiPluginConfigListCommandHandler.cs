using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Template.Models;

namespace MoAI.Wiki.Plugins.Template.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiPluginConfigListCommand"/>
/// </summary>
public class QueryWikiPluginConfigListCommandHandler : IRequestHandler<QueryWikiPluginConfigListCommand, QueryWikiPluginConfigListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiPluginConfigListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiPluginConfigListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiPluginConfigListCommandResponse> Handle(QueryWikiPluginConfigListCommand request, CancellationToken cancellationToken)
    {
        var configs = await _databaseContext.WikiPluginConfigs
            .Where(x => x.WikiId == request.WikiId && x.PluginType == request.PluginKey)
            .Select(x => new
            {
                Id = x.Id,
                Title = x.Title,
                WikiId = x.WikiId,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        var items = configs.Select(x => new WikiPluginConfigSimpleItem
        {
            ConfigId = x.Id,
            Title = x.Title,
            WikiId = x.WikiId,
            CreateTime = x.CreateTime,
            CreateUserId = x.CreateUserId,
            UpdateUserId = x.UpdateUserId,
            UpdateTime = x.UpdateTime
        }).ToList();

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = items
        });

        return new QueryWikiPluginConfigListCommandResponse
        {
            Items = items
        };
    }
}
