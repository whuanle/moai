using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.TeamPlugins.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamPluginDetailCommand"/>
/// </summary>
public class QueryTeamPluginDetailCommandHandler : IRequestHandler<QueryTeamPluginDetailCommand, QueryTeamPluginDetailCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPluginDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryTeamPluginDetailCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamPluginDetailCommandResponse> Handle(QueryTeamPluginDetailCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _dbContext.Plugins
            .Where(x => x.Id == request.PluginId && x.TeamId == request.TeamId)
            .Join(_dbContext.PluginCustoms, a => a.PluginId, b => b.Id, (x, y) => new
            {
                Plugin = x,
                Custom = y
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        var result = new QueryTeamPluginDetailCommandResponse
        {
            PluginId = plugin.Plugin.Id,
            PluginName = plugin.Plugin.PluginName,
            Title = plugin.Plugin.Title,
            Description = plugin.Plugin.Description,
            Type = (PluginType)plugin.Plugin.Type,
            ClassifyId = plugin.Plugin.ClassifyId,
            IsPublic = plugin.Plugin.IsPublic,
            Server = plugin.Custom.Server,
            Headers = plugin.Custom.Headers.JsonToObject<KeyValueString[]>() ?? Array.Empty<KeyValueString>(),
            Queries = plugin.Custom.Queries.JsonToObject<KeyValueString[]>() ?? Array.Empty<KeyValueString>(),
            OpenapiFileId = plugin.Custom.OpenapiFileId,
            OpenapiFileName = plugin.Custom.OpenapiFileName,
            CreateTime = plugin.Plugin.CreateTime,
            CreateUserId = plugin.Plugin.CreateUserId,
            UpdateTime = plugin.Plugin.UpdateTime,
            UpdateUserId = plugin.Plugin.UpdateUserId,
        };

        await _mediator.Send(new FillUserInfoCommand { Items = new[] { result } });

        return result;
    }
}
