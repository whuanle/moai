using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.InternalPluginQueries;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryInternalPluginDetailCommand"/>
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
                Header = TextToJsonExtensions.JsonToObject<IReadOnlyCollection<KeyValueString>>(x.Headers)!,
                Query = TextToJsonExtensions.JsonToObject<IReadOnlyCollection<KeyValueString>>(x.Queries)!,
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
