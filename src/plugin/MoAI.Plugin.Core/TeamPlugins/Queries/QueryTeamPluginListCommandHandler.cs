using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.TeamPlugins.Queries;
using MoAI.Plugin.TeamPlugins.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamPluginListCommand"/>
/// </summary>
public class QueryTeamPluginListCommandHandler : IRequestHandler<QueryTeamPluginListCommand, QueryTeamPluginListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryTeamPluginListCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamPluginListCommandResponse> Handle(QueryTeamPluginListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Plugins
            .Where(x => x.TeamId == request.TeamId);

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

        var plugins = await query
            .Join(_dbContext.PluginCustoms, a => a.PluginId, b => b.Id, (x, y) => new PluginBaseInfoItem
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
                CreateUserId = x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                IsPublic = x.IsPublic,
                ClassifyId = x.ClassifyId
            }).ToArrayAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand { Items = plugins }, cancellationToken);

        return new QueryTeamPluginListCommandResponse { Items = plugins };
    }
}
