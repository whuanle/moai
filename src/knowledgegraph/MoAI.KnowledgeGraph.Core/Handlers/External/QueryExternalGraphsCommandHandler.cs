using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalGraphsCommand"/>
/// </summary>
public class QueryExternalGraphsCommandHandler : IRequestHandler<QueryExternalGraphsCommand, QueryExternalGraphsResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalGraphsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryExternalGraphsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalGraphsResponse> Handle(QueryExternalGraphsCommand request, CancellationToken cancellationToken)
    {
        // 外部接口只暴露 managed（平台托管）图谱，connected 图谱不对外；
        // 资源 key（moai-tr-）绑定单一图谱时只返回该图.
        var items = await _databaseContext.KnowledgeGraphs
            .Where(x => (long)x.TeamId == request.Caller.TeamId && x.Mode == KnowledgeGraphModes.Managed)
            .Where(x => request.Caller.OnlyGraphId == null || x.Id == request.Caller.OnlyGraphId)
            .OrderBy(x => x.Id)
            .Select(x => new ExternalGraphItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Mode = x.Mode,
            })
            .ToListAsync(cancellationToken);

        return new QueryExternalGraphsResponse { Items = items };
    }
}
