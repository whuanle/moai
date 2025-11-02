using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginClassifyListCommand"/>
/// </summary>
public class QueryPluginClassifyListCommandHandler : IRequestHandler<QueryPluginClassifyListCommand, QueryPluginClassifyListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginClassifyListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryPluginClassifyListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginClassifyListCommandResponse> Handle(QueryPluginClassifyListCommand request, CancellationToken cancellationToken)
    {
        var classifies = await _dbContext.Classifies.Where(x => x.Type == "plugin")
            .Select(x => new PluginClassifyItem
            {
                ClassifyId = x.Id,
                Name = x.Name,
                Description = x.Description
            })
            .ToArrayAsync();

        return new QueryPluginClassifyListCommandResponse
        {
            Items = classifies
        };
    }
}