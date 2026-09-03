using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryCustomPluginListCommand"/>
/// </summary>
public class QueryCustomPluginListCommandHandler : IRequestHandler<QueryCustomPluginListCommand, QueryCustomPluginBaseListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCustomPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryCustomPluginListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryCustomPluginBaseListCommandResponse> Handle(QueryCustomPluginListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Plugins.Where(x => x.TeamId == 0).AsQueryable();

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.PluginName.Contains(request.Name));
        }

        if (request.Type.HasValue)
        {
            if (request.Type != PluginType.MCP && request.Type != PluginType.OpenApi)
            {
                throw new BusinessException("插件类型错误");
            }

            query = query.Where(x => x.Type == (int)request.Type.Value);
        }
        else
        {
            query = query.Where(x => x.Type == (int)PluginType.MCP || x.Type == (int)PluginType.OpenApi);
        }

        if (request.ClassifyId.HasValue)
        {
            query = query.Where(x => x.ClassifyId == request.ClassifyId.Value);
        }

        if (request.IsPublic.HasValue)
        {
            query = query.Where(x => x.IsPublic == request.IsPublic.Value);
        }

        var plugins = await query.DynamicOrder(request.OrderByFields)
            .Join(_databaseContext.PluginCustoms, a => a.PluginId, b => b.Id, (x, y) => new PluginBaseInfoItem
            {
                PluginId = x.Id,
                Server = y.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = y.OpenapiFileId,
                OpenapiFileName = y.OpenapiFileName,
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
                IsPublic = x.IsPublic,
                Counter = x.Counter,
                ClassifyId = x.ClassifyId,
            })
            .ToListAsync(cancellationToken);

        return new QueryCustomPluginBaseListCommandResponse { Items = plugins };
    }
}
