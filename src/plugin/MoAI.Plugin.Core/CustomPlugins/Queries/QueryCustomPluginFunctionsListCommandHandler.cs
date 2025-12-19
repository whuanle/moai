using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.CustomPlugins.Queries.Responses;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// <inheritdoc cref="QueryCustomPluginFunctionsListCommand"/>
/// </summary>
public class QueryCustomPluginFunctionsListCommandHandler : IRequestHandler<QueryCustomPluginFunctionsListCommand, QueryCustomPluginFunctionsListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCustomPluginFunctionsListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryCustomPluginFunctionsListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryCustomPluginFunctionsListCommandResponse> Handle(QueryCustomPluginFunctionsListCommand request, CancellationToken cancellationToken)
    {
        var pluginCustomId = await _dbContext.Plugins
            .Where(x => x.Id == request.PluginId)
            .Select(x => x.PluginId)
            .FirstOrDefaultAsync(cancellationToken);

        if (pluginCustomId == 0)
        {
            throw new BusinessException("插件不存在");
        }

        var plugins = await _dbContext.PluginFunctions.Where(x => x.PluginCustomId == pluginCustomId)
            .Select(x => new PluginFunctionItem
            {
                PluginId = x.PluginCustomId,
                FunctionId = x.Id,
                Name = x.Name,
                Path = x.Path,
                Summary = x.Summary,
            }).ToArrayAsync();

        return new QueryCustomPluginFunctionsListCommandResponse { Items = plugins };
    }
}