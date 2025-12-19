using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// <inheritdoc cref="QueryCustomPluginInfoListCommand"/>
/// </summary>
public class QueryCustomPluginInfoListCommandHandler : IRequestHandler<QueryCustomPluginInfoListCommand, QueryCustomPluginInfoListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCustomPluginInfoListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryCustomPluginInfoListCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryCustomPluginInfoListCommandResponse> Handle(QueryCustomPluginInfoListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Plugins.AsQueryable();
        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.PluginName.Contains(request.Name));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(x => x.Type == (int)request.Type.Value);
        }

        if (request.PluginIds != null && request.PluginIds.Count > 0)
        {
            query = query.Where(x => request.PluginIds.Contains(x.Id));
        }

        var plugins = await query
            .Select(x => new PluginBaseInfoItem
            {
                PluginId = x.Id,
                PluginName = x.PluginName,
                Title = x.Title,
                Type = (PluginType)x.Type,
                Description = x.Description,
                IsPublic = x.IsPublic,
                ClassifyId = x.ClassifyId,
            }).ToArrayAsync();

        return new QueryCustomPluginInfoListCommandResponse { Items = plugins };
    }
}
