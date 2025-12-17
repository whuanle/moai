using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Feishu.Models;

namespace MoAI.Wiki.Plugins.Feishu.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiFeishuPluginConfigListCommand"/>
/// </summary>
public class QueryWikiFeishuPluginConfigListCommandHandler : IRequestHandler<QueryWikiFeishuPluginConfigListCommand, QueryWikiFeishuPluginConfigListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiFeishuPluginConfigListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiFeishuPluginConfigListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiFeishuPluginConfigListCommandResponse> Handle(QueryWikiFeishuPluginConfigListCommand request, CancellationToken cancellationToken)
    {
        var configs = await _databaseContext.WikiPluginConfigs
            .Where(x => x.WikiId == request.WikiId && x.PluginType == "feishu")
            .Select(x => new
            {
                Id = x.Id,
                Title = x.Title,
                WikiId = x.WikiId,
                Count = _databaseContext.WikiPluginConfigDocuments.Where(a => a.ConfigId == x.Id).Count(),
                x.WorkState,
                x.WorkMessage,
                Config = x.Config,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        var items = configs.Select(x =>
        {
            var config = x.Config.JsonToObject<WikiFeishuConfig>();
            return new WikiFeishuPluginConfigSimpleItem
            {
                ConfigId = x.Id,
                Title = x.Title,
                WikiId = x.WikiId,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
                SpaceId = config?.SpaceId!,
                ParentNodeToken = config?.ParentNodeToken!,
                PageCount = x.Count,
                WorkMessage = x.WorkMessage,
                WorkState = (WorkerState)x.WorkState
            };
        }).ToList();

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = items
        });

        return new QueryWikiFeishuPluginConfigListCommandResponse
        {
            Items = items
        };
    }
}
