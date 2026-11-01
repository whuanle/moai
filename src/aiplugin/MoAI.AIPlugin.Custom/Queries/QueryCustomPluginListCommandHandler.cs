using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
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
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCustomPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryCustomPluginListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
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

        var rows = await query.DynamicOrder(request.OrderByFields)
            .Join(_databaseContext.PluginCustoms, a => a.PluginId, b => b.Id, (x, y) => new { x, y })
            .ToListAsync(cancellationToken);

        var customIds = rows.Select(r => r.y.Id).ToList();
        var functionCounts = await _databaseContext.PluginFunctions
            .Where(f => customIds.Contains(f.PluginCustomId))
            .GroupBy(f => f.PluginCustomId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var plugins = rows.Select(r => new PluginBaseInfoItem
        {
            PluginId = r.x.Id,
            Server = r.y.Server,
            PluginName = r.x.PluginName,
            AvatarPath = r.x.AvatarPath ?? string.Empty,
            Title = r.x.Title,
            OpenapiFileId = r.y.OpenapiFileId,
            OpenapiFileName = r.y.OpenapiFileName,
            Type = (PluginType)r.x.Type,
            Description = r.x.Description,
            CreateTime = r.x.CreateTime,
            CreateUserId = (int)r.x.CreateUserId,
            UpdateTime = r.x.UpdateTime,
            UpdateUserId = (int)r.x.UpdateUserId,
            IsPublic = r.x.IsPublic,
            Counter = r.x.Counter,
            ClassifyId = r.x.ClassifyId,
            FunctionCount = functionCounts.GetValueOrDefault(r.y.Id),
        })
        .ToList();

        await _userInfoFillService.FillAsync(plugins, cancellationToken);

        return new QueryCustomPluginBaseListCommandResponse { Items = plugins };
    }
}
