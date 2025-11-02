using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;

namespace MoAIPlugin.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginFunctionsListCommand"/>
/// </summary>
public class QueryPluginFunctionsListCommandHandler : IRequestHandler<QueryPluginFunctionsListCommand, QueryPluginFunctionsListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginFunctionsListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryPluginFunctionsListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginFunctionsListCommandResponse> Handle(QueryPluginFunctionsListCommand request, CancellationToken cancellationToken)
    {
        var plugins = await _dbContext.PluginFunctions.Where(x => x.PluginId == request.PluginId)
            .Select(x => new PluginFunctionItem
            {
                PluginId = x.PluginId,
                FunctionId = x.Id,
                Name = x.Name,
                Path = x.Path,
                Summary = x.Summary,
            }).ToArrayAsync();

        return new QueryPluginFunctionsListCommandResponse { Items = plugins };
    }
}