using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Statistics.Queries;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Handlers;

/// <summary>
/// <inheritdoc cref="QueryResourceUsageNamesCommand"/>
/// </summary>
public class QueryResourceUsageNamesCommandHandler : IRequestHandler<QueryResourceUsageNamesCommand, QueryResourceUsageNamesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResourceUsageNamesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryResourceUsageNamesCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryResourceUsageNamesCommandResponse> Handle(QueryResourceUsageNamesCommand request, CancellationToken cancellationToken)
    {
        var names = await _databaseContext.ResourceUsageTotals
            .AsNoTracking()
            .Select(x => x.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return new QueryResourceUsageNamesCommandResponse { Items = names };
    }
}
