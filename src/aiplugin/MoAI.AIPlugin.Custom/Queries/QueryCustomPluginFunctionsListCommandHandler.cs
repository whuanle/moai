using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryCustomPluginFunctionsListCommand"/>
/// </summary>
public class QueryCustomPluginFunctionsListCommandHandler : IRequestHandler<QueryCustomPluginFunctionsListCommand, QueryCustomPluginFunctionsListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCustomPluginFunctionsListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryCustomPluginFunctionsListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryCustomPluginFunctionsListCommandResponse> Handle(QueryCustomPluginFunctionsListCommand request, CancellationToken cancellationToken)
    {
        var pluginCustomId = await _databaseContext.Plugins
            .Where(x => x.Id == request.PluginId)
            .Select(x => x.PluginId)
            .FirstOrDefaultAsync(cancellationToken);

        if (pluginCustomId == Guid.Empty)
        {
            throw new BusinessException("插件不存在");
        }

        var plugins = await _databaseContext.PluginFunctions
            .Where(x => x.PluginCustomId == pluginCustomId)
            .Select(x => new PluginFunctionItem
            {
                PluginId = x.PluginCustomId,
                FunctionId = x.Id,
                Name = x.Name,
                Path = x.Path,
                Summary = x.Summary,
            })
            .ToListAsync(cancellationToken);

        return new QueryCustomPluginFunctionsListCommandResponse { Items = plugins };
    }
}
