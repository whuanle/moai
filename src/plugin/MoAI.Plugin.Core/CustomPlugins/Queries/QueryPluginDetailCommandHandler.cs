using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries;
using MoAI.User.Queries;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// <inheritdoc cref="QueryNativePluginDetailCommand"/>
/// </summary>
public class QueryPluginDetailCommandHandler : IRequestHandler<QueryPluginDetailCommand, QueryPluginDetailCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryPluginDetailCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginDetailCommandResponse> Handle(QueryPluginDetailCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _dbContext.Plugins.Where(x => x.Id == request.PluginId)
            .Select(x => new QueryPluginDetailCommandResponse
            {
                PluginId = x.Id,
                Server = x.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = x.OpenapiFileId,
                OpenapiFileName = x.OpenapiFileName,
                Header = x.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
                Query = x.Queries.JsonToObject<IReadOnlyCollection<KeyValueString>>()!,
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                IsPublic = x.IsPublic,
                ClassifyId = x.ClassifyId
            }).FirstOrDefaultAsync();

        if (plugin == null)
        {
            throw new BusinessException("未找到插件") { StatusCode = 404 };
        }

        await _mediator.Send(new FillUserInfoCommand { Items = new Responses.QueryPluginDetailCommandResponse[] { plugin } });

        return plugin;
    }
}
