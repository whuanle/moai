using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryCustomPluginDetailCommand"/>
/// </summary>
public class QueryCustomPluginDetailCommandHandler : IRequestHandler<QueryCustomPluginDetailCommand, QueryCustomPluginDetailCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCustomPluginDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryCustomPluginDetailCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryCustomPluginDetailCommandResponse> Handle(QueryCustomPluginDetailCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .Where(x => x.Id == request.PluginId)
            .Join(_databaseContext.PluginCustoms, a => a.PluginId, b => b.Id, (x, y) => new QueryCustomPluginDetailCommandResponse
            {
                PluginId = x.Id,
                Server = y.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = y.OpenapiFileId,
                OpenapiFileName = y.OpenapiFileName,
                Header = y.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>() ?? Array.Empty<KeyValueString>(),
                Query = y.Queries.JsonToObject<IReadOnlyCollection<KeyValueString>>() ?? Array.Empty<KeyValueString>(),
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
                IsPublic = x.IsPublic,
                ClassifyId = x.ClassifyId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("未找到插件") { StatusCode = 404 };
        }

        return plugin;
    }
}
